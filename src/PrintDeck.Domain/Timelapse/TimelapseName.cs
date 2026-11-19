using System.Text;

namespace PrintDeck.Domain.Timelapse;

/// <summary>Names of rendered timelapses: <c>benchy_20261119-143000.mp4</c>, from the printed file and when the print started.</summary>
public static class TimelapseName
{
	public const string Extension = ".mp4";

	/// <summary>The video name for a print of <paramref name="fileName"/> (which may sit in a folder) started at <paramref name="startedAt"/>.</summary>
	public static string For(string fileName, DateTimeOffset startedAt)
	{
		string stem = Path.GetFileNameWithoutExtension(fileName.Replace('\\', '/').Split('/')[^1]);
		StringBuilder clean = new(stem.Length);
		foreach (char c in stem)
		{
			char keep = char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '_';
			if (keep != '_' || clean.Length == 0 || clean[^1] != '_') clean.Append(keep);
		}
		string safe = clean.ToString().Trim('.', '_');
		if (safe.Length == 0) safe = "print";
		if (safe.Length > 80) safe = safe[..80];
		return $"{safe}_{startedAt.ToLocalTime():yyyyMMdd-HHmmss}{Extension}";
	}

	/// <summary>True for a plain video name: no folders, not hidden, ends in .mp4. Anything else is never served or deleted.</summary>
	public static bool IsValid(string? name) =>
		!string.IsNullOrWhiteSpace(name)
		&& name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
		&& name.Length > Extension.Length
		&& !name.StartsWith('.')
		&& name.IndexOfAny(['/', '\\', ':']) < 0
		&& !name.Any(char.IsControl);
}
