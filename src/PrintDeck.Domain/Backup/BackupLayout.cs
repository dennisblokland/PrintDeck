using PrintDeck.Domain.Files;

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

	/// <summary>Sidecars kept next to an upload: its analysis, its finished prints and its slicer thumbnail.</summary>
	public static readonly IReadOnlyList<string> SidecarSuffixes = [".meta.json", ".prints.json", ".thumb.png", ".thumb.jpg"];

	/// <summary>The download name, e.g. <c>printdeck-backup-20261117-1530.zip</c>.</summary>
	public static string FileName(DateTimeOffset createdAt) => $"printdeck-backup-{createdAt.UtcDateTime:yyyyMMdd-HHmm}.zip";

	/// <summary>
	/// The upload path for an archive entry under <c>uploads/</c>, folders included ("parts/cube.gcode"); null for
	/// anything else: "..", hidden files or folders, backslashes, half-written ".part" files.
	/// </summary>
	public static string? UploadName(string entry)
	{
		if (!entry.StartsWith(UploadsPrefix, StringComparison.Ordinal)) return null;
		string name = entry[UploadsPrefix.Length..];
		if (name.Length == 0 || name.EndsWith(".part", StringComparison.Ordinal) || name.IndexOfAny(['\\', ':', '\0']) >= 0) return null;
		string[] parts = name.Split('/');
		bool plain = parts.Length <= FilePath.MaxDepth + 1
			&& parts.All(p => p.Length > 0 && !p.StartsWith('.') && p.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
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
