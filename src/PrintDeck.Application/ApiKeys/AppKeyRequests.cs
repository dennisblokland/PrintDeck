using System.Security.Cryptography;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.ApiKeys;

/// <summary>
/// App-key requests in memory (OctoPrint's app-key flow): an app asks without signing in, an admin allows or denies it,
/// and the app picks the key up once. Requests are forgotten after a few minutes, and a restart forgets them all.
/// </summary>
public sealed class AppKeyRequests(TimeProvider time)
{
	/// <summary>Home Assistant waits 5 minutes for an answer.</summary>
	public static readonly TimeSpan PendingFor = TimeSpan.FromMinutes(5);

	/// <summary>How long an allowed key waits for the app to pick it up.</summary>
	public static readonly TimeSpan PickUpWithin = TimeSpan.FromMinutes(2);

	/// <summary>Anyone can ask, so the number waiting is capped.</summary>
	public const int MaxPending = 10;

	public const string TooMany = "Too many app key requests are waiting; try again in a few minutes.";

	private sealed record Entry(AppKeyRequest Request, string? Key, DateTimeOffset? GrantedAt);

	private readonly Lock _gate = new();
	private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

	public Result<AppKeyRequest> Add(string app, string? user)
	{
		string name = app?.Trim() ?? "";
		string? account = string.IsNullOrWhiteSpace(user) ? null : user.Trim();
		if (name.Length is 0 or > AppKeyRequest.MaxNameLength || name.Any(char.IsControl))
			return Result.Failure<AppKeyRequest>("Give the app a name of up to 64 characters.");
		if (account is { Length: > 256 } || account?.Any(char.IsControl) == true)
			return Result.Failure<AppKeyRequest>("That is not a user name.");

		lock (_gate)
		{
			Prune();
			if (_entries.Values.Count(e => e.Key is null) >= MaxPending) return Result.Failure<AppKeyRequest>(TooMany);
			AppKeyRequest request = new(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), name, account, time.GetUtcNow());
			_entries[request.Token] = new Entry(request, null, null);
			return Result.Success(request);
		}
	}

	/// <summary>Requests waiting for an admin, oldest first.</summary>
	public IReadOnlyList<AppKeyRequest> Pending()
	{
		lock (_gate)
		{
			Prune();
			return _entries.Values.Where(e => e.Key is null).Select(e => e.Request).OrderBy(r => r.RequestedAt).ToList();
		}
	}

	public AppKeyRequest? FindPending(string token)
	{
		lock (_gate)
		{
			Prune();
			return _entries.TryGetValue(token, out Entry? e) && e.Key is null ? e.Request : null;
		}
	}

	/// <summary>What the app learns when it asks again. A granted key is handed out once and then forgotten.</summary>
	public (AppKeyRequestState State, string? Key) Poll(string token)
	{
		lock (_gate)
		{
			Prune();
			if (!_entries.TryGetValue(token, out Entry? e)) return (AppKeyRequestState.Gone, null);
			if (e.Key is null) return (AppKeyRequestState.Pending, null);
			_entries.Remove(token);
			return (AppKeyRequestState.Granted, e.Key);
		}
	}

	/// <summary>False when the request is no longer waiting.</summary>
	public bool Grant(string token, string key)
	{
		lock (_gate)
		{
			Prune();
			if (!_entries.TryGetValue(token, out Entry? e) || e.Key is not null) return false;
			_entries[token] = e with { Key = key, GrantedAt = time.GetUtcNow() };
			return true;
		}
	}

	/// <summary>False when the request is no longer waiting.</summary>
	public bool Deny(string token)
	{
		lock (_gate)
		{
			Prune();
			return _entries.TryGetValue(token, out Entry? e) && e.Key is null && _entries.Remove(token);
		}
	}

	private void Prune()
	{
		DateTimeOffset now = time.GetUtcNow();
		foreach ((string token, Entry e) in _entries.ToList())
			if (e.GrantedAt is { } granted ? now - granted > PickUpWithin : now - e.Request.RequestedAt > PendingFor)
				_entries.Remove(token);
	}
}
