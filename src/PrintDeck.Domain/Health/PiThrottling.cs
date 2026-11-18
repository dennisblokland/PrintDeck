using System.Globalization;

using PrintDeck.Domain.Health.Enums;

namespace PrintDeck.Domain.Health;

/// <summary>
/// The Raspberry Pi firmware's throttle flags, from <c>vcgencmd get_throttled</c> (<c>throttled=0x50005</c>) or
/// sysfs <c>get_throttled</c> (<c>50005</c>). Bits 0-3 are now, bits 16-19 since boot: under-voltage,
/// ARM frequency capped, throttled, soft temperature limit.
/// </summary>
public sealed record PiThrottling(int Flags)
{
	public bool UnderVoltageNow => (Flags & 0x1) != 0;
	public bool LimitedNow => (Flags & 0xE) != 0;
	public bool UnderVoltageSinceBoot => (Flags & 0x10000) != 0;
	public bool LimitedSinceBoot => (Flags & 0xE0000) != 0;

	public static PiThrottling? Parse(string? text)
	{
		string value = (text ?? "").Trim();
		int eq = value.IndexOf('=');
		if (eq >= 0) value = value[(eq + 1)..].Trim();
		if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value[2..];
		return int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int flags) ? new PiThrottling(flags) : null;
	}

	public IReadOnlyList<HealthWarning> Check()
	{
		List<HealthWarning> warnings = [];
		if (UnderVoltageNow)
			warnings.Add(new("pi-undervoltage", HealthSeverity.Error, "The Raspberry Pi has too little power",
				"Under-voltage right now. It causes serial errors that stop prints and can corrupt the SD card. Use a 5.1 V supply made for the Pi and a short, thick cable."));
		else if (UnderVoltageSinceBoot)
			warnings.Add(new("pi-undervoltage", HealthSeverity.Warning, "The Raspberry Pi had too little power",
				"Under-voltage happened since the last boot. It causes serial errors that stop prints; check the power supply and cable."));

		if (LimitedNow)
			warnings.Add(new("pi-throttled", HealthSeverity.Warning, "The Raspberry Pi is slowed down",
				"It is throttled right now, from heat or low power. Streaming G-code may stutter; add cooling or a better supply."));
		else if (LimitedSinceBoot)
			warnings.Add(new("pi-throttled", HealthSeverity.Warning, "The Raspberry Pi was slowed down",
				"It was throttled since the last boot, from heat or low power. Add cooling or check the supply."));
		return warnings;
	}
}
