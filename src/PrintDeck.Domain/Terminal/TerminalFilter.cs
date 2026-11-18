using System.Text.RegularExpressions;

namespace PrintDeck.Domain.Terminal;

/// <summary>
/// Hides terminal lines whose text matches <see cref="Pattern"/>, a case-insensitive regular expression.
/// Sent lines are matched as they went over the wire, with line number and checksum ("N12 M105*39").
/// </summary>
public sealed record TerminalFilter(string Name, string Pattern, bool Enabled = true)
{
	public const int MaxNameLength = 64;
	public const int MaxPatternLength = 500;

	private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
	private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(50);

	/// <summary>A command as sent, optionally with a line number and checksum.</summary>
	private static string Command(string code) => $@"^\s*(N\d+\s+)?{code}(\s|\*|$)";

	/// <summary>What a new installation starts with; temperature reports and bare <c>ok</c>s are hidden, the rest shown.</summary>
	public static IReadOnlyList<TerminalFilter> Defaults { get; } =
	[
		new("Temperature reports", $@"{Command("M105")}|^\s*(ok\s+)?T\d*:"),
		new("Bare ok", @"^ok\s*$"),
		new("SD status", $@"{Command("M27")}|^SD printing byte|^Not SD printing", Enabled: false),
		new("Position reports", $@"{Command("M114")}|^X:-?\d", Enabled: false),
		new("Busy", @"^(echo:\s*)?busy:", Enabled: false),
		new("Wait", @"^wait\s*$", Enabled: false)
	];

	/// <summary>Whether the line is hidden by this filter. A pattern that is invalid or too slow hides nothing.</summary>
	public bool Matches(string text)
	{
		if (string.IsNullOrEmpty(Pattern)) return false;
		try
		{
			return Regex.IsMatch(text, Pattern, Options, _timeout);
		}
		catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
		{
			return false;
		}
	}

	/// <summary>Whether <paramref name="pattern"/> is a regular expression .NET accepts.</summary>
	public static bool IsValidPattern(string pattern)
	{
		if (string.IsNullOrWhiteSpace(pattern)) return false;
		try
		{
			_ = new Regex(pattern, Options, _timeout);
			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}
}
