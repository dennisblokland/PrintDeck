namespace PrintDeck.Infrastructure.Identity;

/// <summary>An app key row. Only the key's hash is stored; the key goes away with its owner's account.</summary>
public sealed class PrintDeckApiKey
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string UserId { get; set; } = "";
	public PrintDeckUser? User { get; set; }
	public string Name { get; set; } = "";
	/// <summary>SHA-256 of the key, upper-case hex.</summary>
	public string Hash { get; set; } = "";
	/// <summary>The key's last four characters.</summary>
	public string Hint { get; set; } = "";
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset? LastUsedAt { get; set; }
}
