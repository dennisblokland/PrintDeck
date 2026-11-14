namespace PrintDeck.Domain.Protocol;

public static class GcodeLine
{
	/// <summary>Strips comments and whitespace. Returns null when nothing is left to send.</summary>
	public static string? Clean(string raw)
	{
		int semicolon = raw.IndexOf(';');
		string line = (semicolon >= 0 ? raw[..semicolon] : raw).Trim();
		return line.Length == 0 ? null : line;
	}

	/// <summary>Wraps a command with a line number and XOR checksum: "N12 G1 X10*85".</summary>
	public static string WithChecksum(long lineNumber, string command)
	{
		string body = $"N{lineNumber} {command}";
		byte checksum = 0;
		foreach (char c in body) checksum ^= (byte) c;
		return $"{body}*{checksum}";
	}

	/// <summary>Command word of a line ("G1", "M104"), upper-case, or empty.</summary>
	public static string CommandOf(string line)
	{
		ReadOnlySpan<char> span = line.AsSpan().TrimStart();
		int end = span.IndexOfAny(' ', '*');
		return (end < 0 ? span : span[..end]).ToString().ToUpperInvariant();
	}

	/// <summary>Commands that legitimately block for a long time without an "ok".</summary>
	public static bool IsLongRunning(string command) =>
		CommandOf(command) is "G28" or "G29" or "M109" or "M190" or "M191" or "G4" or "M400" or "M303";
}
