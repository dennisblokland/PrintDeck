using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>App keys in a list; every account named in <see cref="Accounts"/> exists.</summary>
internal sealed class FakeApiKeys : IApiKeys
{
	private readonly List<(ApiKeyInfo Info, string Key)> _keys = [];
	private int _next;

	public HashSet<string> Accounts { get; } = ["ada", "vic"];
	public IReadOnlyList<ApiKeyInfo> Keys => _keys.Select(k => k.Info).ToList();

	public Task<IReadOnlyList<ApiKeyInfo>> ListAsync(CancellationToken ct) => Task.FromResult(Keys);

	public Task<string?> CreateAsync(string ownerName, string name, CancellationToken ct)
	{
		if (!Accounts.Contains(ownerName)) return Task.FromResult<string?>(null);
		string key = $"KEY{++_next}";
		_keys.Add((new ApiKeyInfo($"id{_next}", name, ownerName, ApiKeySecret.Hint(key), DateTimeOffset.UnixEpoch, null), key));
		return Task.FromResult<string?>(key);
	}

	public Task<bool> RevokeAsync(string id, CancellationToken ct) => Task.FromResult(_keys.RemoveAll(k => k.Info.Id == id) > 0);

	public Task<ApiKeyOwner?> FindAsync(string key, CancellationToken ct) =>
		Task.FromResult(_keys.FirstOrDefault(k => k.Key == key) is { Info: { } info }
			? new ApiKeyOwner(info.Name, info.OwnerName, UserRole.Admin)
			: null);
}
