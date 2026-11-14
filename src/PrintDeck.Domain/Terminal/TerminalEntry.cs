using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Domain.Terminal;

public sealed record TerminalEntry(long Id, DateTimeOffset At, TerminalDirection Direction, string Text)
{
	public bool IsTemperatureChatter =>
		Text.Contains("T:") && (Text.StartsWith("ok T:") || Text.StartsWith(" T:") || Text.StartsWith("T:"))
		|| IsTemperaturePoll(Text);

	/// <summary>"M105", plain or as sent on the wire with line number and checksum ("N12 M105*39").</summary>
	private static bool IsTemperaturePoll(string text)
	{
		ReadOnlySpan<char> s = text.AsSpan().Trim();
		int star = s.LastIndexOf('*');
		if (star >= 0) s = s[..star].TrimEnd();
		if (s.Length > 1 && s[0] is 'N' or 'n' && char.IsAsciiDigit(s[1]))
		{
			int space = s.IndexOf(' ');
			if (space < 0) return false;
			s = s[(space + 1)..].TrimStart();
		}
		return s.Equals("M105", StringComparison.OrdinalIgnoreCase);
	}
}
