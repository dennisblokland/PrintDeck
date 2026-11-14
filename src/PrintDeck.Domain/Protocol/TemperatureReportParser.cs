using System.Globalization;
using System.Text.RegularExpressions;

using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Domain.Protocol;

/// <summary>Parses Marlin/RepRap style reports, e.g. "ok T:210.1 /210.0 B:60.0 /60.0 @:64 B@:0".</summary>
public static partial class TemperatureReportParser
{
	[GeneratedRegex(@"(?<![A-Za-z@])(?<h>T\d*|B|C):\s*(?<a>-?\d+(?:\.\d+)?)\s*(?:/\s*(?<t>-?\d+(?:\.\d+)?))?")]
	private static partial Regex HeaterPattern();

	public static IReadOnlyList<HeaterReading>? TryParse(string line)
	{
		if (!line.Contains("T:") && !line.Contains("B:")) return null;

		Dictionary<string, HeaterReading> readings = new();
		foreach (Match m in HeaterPattern().Matches(line))
		{
			string heater = m.Groups["h"].Value;
			double actual = double.Parse(m.Groups["a"].Value, CultureInfo.InvariantCulture);
			double target = m.Groups["t"].Success ? double.Parse(m.Groups["t"].Value, CultureInfo.InvariantCulture) : 0;
			readings[heater] = new HeaterReading(heater, actual, target);
		}

		// Single-extruder firmwares report "T:" and multi-extruder ones also include "T0:"; prefer T0.
		if (readings.ContainsKey("T") && readings.ContainsKey("T0")) readings.Remove("T");
		else if (readings.Remove("T", out HeaterReading? t)) readings["T0"] = t with { Heater = "T0" };

		return readings.Count == 0 ? null : readings.Values.OrderBy(r => r.IsBed).ThenBy(r => r.Heater).ToList();
	}
}
