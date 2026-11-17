namespace PrintDeck.Domain.Backup;

/// <summary>The first entry of a backup archive: marks the zip as a PrintDeck backup and says when it was made.</summary>
public sealed record BackupManifest(string Format, int Version, DateTimeOffset CreatedAt, int FileCount)
{
	public const string ExpectedFormat = "printdeck-backup";
	public const int CurrentVersion = 1;

	public static BackupManifest Create(DateTimeOffset createdAt, int fileCount) => new(ExpectedFormat, CurrentVersion, createdAt, fileCount);

	/// <summary>Why this manifest can't be restored, or null when it can.</summary>
	public string? Problem() =>
		Format != ExpectedFormat ? "That file is not a PrintDeck backup."
		: Version > CurrentVersion ? "This backup was made by a newer PrintDeck. Update PrintDeck first."
		: Version < 1 ? "That file is not a PrintDeck backup."
		: null;
}
