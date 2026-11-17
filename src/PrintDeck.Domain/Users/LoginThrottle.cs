using System.Net;
using System.Net.Sockets;

namespace PrintDeck.Domain.Users;

/// <summary>
/// Failed sign-ins per client address over a sliding window. Account lockout stops guessing one account's password;
/// this stops one address from trying passwords across many usernames. Once an address has <paramref name="maxFailures"/>
/// failures inside <paramref name="window"/>, it may not try again until the oldest of them leaves the window.
/// Thread-safe; it remembers at most <paramref name="maxClients"/> addresses.
/// </summary>
public sealed class LoginThrottle(int maxFailures, TimeSpan window, int maxClients = 10_000)
{
	public const string UnknownClient = "unknown";

	private readonly Dictionary<string, Queue<DateTimeOffset>> _failures = new(StringComparer.Ordinal);
	private readonly Lock _lock = new();

	public int MaxFailures => maxFailures;
	public TimeSpan Window => window;

	/// <summary>
	/// The address an attempt counts against. IPv6 counts per /64, the block one home or server gets, since anyone
	/// with one has billions of addresses to rotate through. IPv4-mapped IPv6 counts as the IPv4 address.
	/// </summary>
	public static string ClientKey(IPAddress? address)
	{
		if (address is null) return UnknownClient;
		if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
		if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();

		byte[] bytes = address.GetAddressBytes();
		Array.Clear(bytes, 8, 8);
		return new IPAddress(bytes) + "/64";
	}

	/// <summary>How long <paramref name="client"/> must wait before trying again; null when it may try now.</summary>
	public TimeSpan? RetryAfter(string client, DateTimeOffset now)
	{
		lock (_lock)
		{
			if (!_failures.TryGetValue(client, out Queue<DateTimeOffset>? failures)) return null;
			Expire(client, failures, now);
			return failures.Count >= maxFailures ? failures.Peek() + window - now : null;
		}
	}

	public void RecordFailure(string client, DateTimeOffset now)
	{
		lock (_lock)
		{
			if (!_failures.TryGetValue(client, out Queue<DateTimeOffset>? failures))
			{
				if (_failures.Count >= maxClients) MakeRoom(now);
				_failures[client] = failures = new Queue<DateTimeOffset>();
			}
			failures.Enqueue(now);
			// Attempts beyond the limit are refused before they are tried, so only the newest few matter.
			while (failures.Count > maxFailures) failures.Dequeue();
		}
	}

	private void Expire(string client, Queue<DateTimeOffset> failures, DateTimeOffset now)
	{
		while (failures.Count > 0 && now - failures.Peek() >= window) failures.Dequeue();
		if (failures.Count == 0) _failures.Remove(client);
	}

	/// <summary>Drops expired addresses; when every one is still active, the one whose last failure is oldest.</summary>
	private void MakeRoom(DateTimeOffset now)
	{
		foreach ((string client, Queue<DateTimeOffset> failures) in _failures.ToList())
			Expire(client, failures, now);
		if (_failures.Count < maxClients) return;
		string stalest = _failures.MinBy(f => f.Value.Last()).Key;
		_failures.Remove(stalest);
	}
}
