namespace PrintDeck.Domain.Backup;

/// <summary>
/// What a backup archive (zip) holds: a manifest, <c>settings.json</c> (printer profiles included) and every stored
/// upload with its sidecars under <c>uploads/</c>.
/// </summary>
public static class BackupLayout
{
	public const string ManifestEntry = "printdeck-backup.json";
	public const string SettingsEntry = "settings.json";
	public const string UploadsPrefix = "uploads/";

	/// <summary>Sidecars kept next to an upload: its analysis and its finished prints.</summary>
	public static readonly IReadOnlyList<string> SidecarSuffixes = [".meta.json", ".prints.json"];

	/// <summary>The download name, e.g. <c>printdeck-backup-20261117-1530.zip</c>.</summary>
	public static string FileName(DateTimeOffset createdAt) => $"printdeck-backup-{createdAt.UtcDateTime:yyyyMMdd-HHmm}.zip";

	/// <summary>
	/// The upload file name for an archive entry under <c>uploads/</c>; null for anything that is not a plain file
	/// name there (sub-folders, "..", hidden files, half-written ".part" files).
	/// </summary>
	public static string? UploadName(string entry)
	{
		if (!entry.StartsWith(UploadsPrefix, StringComparison.Ordinal)) return null;
		string name = entry[UploadsPrefix.Length..];
		bool plain = name.Length > 0
			&& !name.StartsWith('.')
			&& !name.EndsWith(".part", StringComparison.Ordinal)
			&& name.IndexOfAny(['/', '\\', ':', '\0']) < 0
			&& name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
		return plain ? name : null;
	}

	/// <summary>The upload a sidecar belongs to ("a.gcode.meta.json" → "a.gcode"); null when <paramref name="name"/> is not a sidecar.</summary>
	public static string? SidecarOwner(string name)
	{
		foreach (string suffix in SidecarSuffixes)
			if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
				return name[..^suffix.Length];
		return null;
	}
}
