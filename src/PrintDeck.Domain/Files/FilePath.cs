namespace PrintDeck.Domain.Files;

/// <summary>
/// Paths of stored files and folders, relative to the uploads folder with '/' between folders:
/// <c>parts/benchy.gcode</c>. The top level is the empty string.
/// </summary>
public static class FilePath
{
	public const int MaxDepth = 8;
	public const int MaxSegmentLength = 120;

	// The same rule uploads always used, so names stored before folders existed stay valid.
	private static readonly char[] _invalid = Path.GetInvalidFileNameChars();

	/// <summary>
	/// A clean path, or null when it can't be one: "..", "." or hidden (".name") parts, too deep or too long.
	/// Backslashes count as '/', spaces around parts are dropped, empty parts are skipped and characters that
	/// can't be in a file name become '_'. Blank means the top level ("").
	/// </summary>
	public static string? Normalize(string? raw)
	{
		List<string> parts = [];
		foreach (string piece in (raw ?? "").Replace('\\', '/').Split('/'))
		{
			string part = piece.Trim();
			if (part.Length == 0) continue;
			if (part.StartsWith('.') || part.Length > MaxSegmentLength) return null;
			foreach (char c in _invalid) part = part.Replace(c, '_');
			parts.Add(part);
		}
		return parts.Count > MaxDepth ? null : string.Join('/', parts);
	}

	/// <summary>The folder a path is in: "" for "benchy.gcode", "parts" for "parts/benchy.gcode".</summary>
	public static string Folder(string path)
	{
		int slash = path.LastIndexOf('/');
		return slash < 0 ? "" : path[..slash];
	}

	/// <summary>The last part: "benchy.gcode" for "parts/benchy.gcode".</summary>
	public static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];

	public static string Combine(string folder, string name) => folder.Length == 0 ? name : $"{folder}/{name}";

	/// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> itself or anywhere inside it.</summary>
	public static bool IsWithin(string path, string folder) =>
		folder.Length == 0 || path == folder || path.StartsWith(folder + "/", StringComparison.Ordinal);
}
