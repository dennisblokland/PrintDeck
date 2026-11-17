using System.Globalization;
using System.Text.RegularExpressions;

namespace PrintDeck.Domain.Protocol;

/// <summary>
/// Feed rate (<c>M220</c>) and flow (<c>M221</c>) percentages, both as sent and as the firmware reports them:
/// Marlin answers <c>M220</c> with "FR:100%" and <c>M221</c> with "echo:E0 Flow: 95%";
/// Prusa firmware reports flow as "E0 extrude factor: 95%".
/// </summary>
public static partial class RateReportParser
{
	[GeneratedRegex(@"^(?:echo:\s*)?FR:\s*(\d+)\s*%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex FeedReport();

	[GeneratedRegex(@"^(?:echo:\s*)?E\d+\s+(?:Flow|extrude factor):\s*(\d+)\s*%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex FlowReport();

	[GeneratedRegex(@"(?:^|\s)S(\d+(?:\.\d+)?)(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex SArgument();

	public static int? TryParseFeedReport(string line) => Percent(FeedReport().Match(line));

	public static int? TryParseFlowReport(string line) => Percent(FlowReport().Match(line));

	/// <summary>The percentage an outgoing <c>M220 S…</c> sets; null for other lines or M220 without S.</summary>
	public static int? FeedSetBy(string command) => SetBy(command, "M220");

	/// <summary>The percentage an outgoing <c>M221 S…</c> sets; null for other lines or M221 without S.</summary>
	public static int? FlowSetBy(string command) => SetBy(command, "M221");

	private static int? SetBy(string command, string code)
	{
		if (GcodeLine.CommandOf(command) != code) return null;
		Match m = SArgument().Match(command);
		return m.Success ? (int)Math.Round(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) : null;
	}

	private static int? Percent(Match m) =>
		m.Success && int.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out int v) ? v : null;
}
