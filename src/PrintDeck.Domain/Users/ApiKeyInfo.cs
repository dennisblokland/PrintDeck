namespace PrintDeck.Domain.Users;

/// <summary>An app key as the Settings page lists it; never the key itself.</summary>
/// <param name="OwnerName">The account the key acts as; it has that account's role.</param>
/// <param name="Hint">The key's last four characters.</param>
public sealed record ApiKeyInfo(string Id, string Name, string OwnerName, string Hint, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);
