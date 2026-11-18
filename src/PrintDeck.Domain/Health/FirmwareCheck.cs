using System.Text.RegularExpressions;

using PrintDeck.Domain.Health.Enums;

namespace PrintDeck.Domain.Health;

/// <summary>
/// Firmware known to cause trouble, by the name it reports in <c>M115</c> (before <c>SOURCE_CODE_URL</c>).
/// Marlin before 1.1.0, release candidates included, made thermal runaway protection optional, and many printers
/// (Anet, early Anycubic and Creality) shipped with it off: a loose thermistor can then heat the hotend without limit.
/// </summary>
public static partial class FirmwareCheck
{
	[GeneratedRegex(@"\bMarlin\s+V?(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?(?<pre>[-_ ]?RC\d*)?", RegexOptions.IgnoreCase)]
	private static partial Regex MarlinVersion();

	public static IReadOnlyList<HealthWarning> Check(string? firmware)
	{
		if (string.IsNullOrWhiteSpace(firmware)) return [];
		List<HealthWarning> warnings = [];

		if (MarlinVersion().Match(firmware) is { Success: true } m && IsBefore110(m))
			warnings.Add(new("firmware-thermal-runaway", HealthSeverity.Warning, $"Old firmware: {firmware}",
				"Marlin before 1.1.0 often shipped with thermal runaway protection switched off. Check that it is on, or update the firmware."));

		return warnings;
	}

	private static bool IsBefore110(Match m)
	{
		int major = int.Parse(m.Groups["major"].Value);
		int minor = int.Parse(m.Groups["minor"].Value);
		int patch = m.Groups["patch"].Success ? int.Parse(m.Groups["patch"].Value) : 0;
		if (major != 1) return major < 1;
		if (minor != 1) return minor < 1;
		return patch == 0 && m.Groups["pre"].Success; // 1.1.0-RC8 and the like
	}
}
