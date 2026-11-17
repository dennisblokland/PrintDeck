using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>App keys: one revocable key per slicer or integration, each acting as the account that made it.</summary>
public interface IApiKeys
{
	/// <summary>Every key, oldest first.</summary>
	Task<IReadOnlyList<ApiKeyInfo>> ListAsync(CancellationToken ct);

	/// <summary>Stores a new key for <paramref name="ownerName"/>; returns the key, which can't be read back later. Null when there is no such account.</summary>
	Task<string?> CreateAsync(string ownerName, string name, CancellationToken ct);

	/// <summary>False when there was no such key.</summary>
	Task<bool> RevokeAsync(string id, CancellationToken ct);

	/// <summary>Who <paramref name="key"/> signs in as, noting when it was used; null for an unknown or revoked key.</summary>
	Task<ApiKeyOwner?> FindAsync(string key, CancellationToken ct);
}
