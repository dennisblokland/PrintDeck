using System.Globalization;

using PrintDeck.Domain.SdCard;

namespace PrintDeck.Domain.Protocol;

/// <summary>Reads Marlin's SD card replies: the <c>M20</c> file list and <c>M27</c> progress.</summary>
public static class SdCardReportParser
{
	public const string ListBegin = "Begin file list";
	public const string ListEnd = "End file list";
	public const string PrintDone = "Done printing file";

	/// <summary>
	/// One line of an <c>M20</c> list: "CUBE.GCO 1234" or, with long names, "CUBE~1.GCO 1234 Cube 0.2mm.gcode".
	/// Folders ("DIR/") and non-G-code files are skipped.
	/// </summary>
	public static SdFile? TryParseFileEntry(string line)
	{
		string text = line.Trim();
		if (text.Length == 0) return null;

		string[] parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
		string path = parts[0];
		if (path.EndsWith('/') || !IsGcode(path)) return null;

		long size = 0;
		string? longName = null;
		if (parts.Length > 1)
		{
			if (long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long s)) size = s;
			if (parts.Length > 2) longName = parts[2].Trim();
		}
		return new SdFile(path, size, longName);
	}

	/// <summary>"SD printing byte 1234/56789" → (1234, 56789).</summary>
	public static (long Done, long Total)? TryParseProgress(string line)
	{
		const string prefix = "SD printing byte ";
		int start = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
		if (start < 0) return null;

		ReadOnlySpan<char> rest = line.AsSpan(start + prefix.Length).Trim();
		int slash = rest.IndexOf('/');
		if (slash < 0) return null;

		return long.TryParse(rest[..slash], NumberStyles.None, CultureInfo.InvariantCulture, out long done)
			   && long.TryParse(rest[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out long total)
			? (done, total)
			: null;
	}

	/// <summary>"SD card ok" (after <c>M21</c> or when a card is inserted).</summary>
	public static bool IsCardReady(string line) => Contains(line, "SD card ok");

	/// <summary>"SD init fail", "No SD card", "No media", "SD card released" / "removed".</summary>
	public static bool IsCardGone(string line) =>
		Contains(line, "SD init fail") || Contains(line, "No SD card") || Contains(line, "SD card released") || Contains(line, "SD card removed")
		|| Contains(line, "No media");

	private static bool IsGcode(string path) =>
		path.EndsWith(".GCO", StringComparison.OrdinalIgnoreCase)
		|| path.EndsWith(".G", StringComparison.OrdinalIgnoreCase)
		|| path.EndsWith(".GCODE", StringComparison.OrdinalIgnoreCase);

	private static bool Contains(string line, string text) => line.Contains(text, StringComparison.OrdinalIgnoreCase);
}
