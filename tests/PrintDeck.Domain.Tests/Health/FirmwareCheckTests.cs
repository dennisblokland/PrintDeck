using PrintDeck.Domain.Health;
using PrintDeck.Domain.Health.Enums;

namespace PrintDeck.Domain.Tests.Health;

public class FirmwareCheckTests
{
	[Theory]
	[InlineData("Marlin 1.0.2")]
	[InlineData("Marlin V1.0.2")]
	[InlineData("Marlin 1.0.2-2")]
	[InlineData("Marlin 1.1.0-RC8")]
	[InlineData("Marlin 1.1.0 RC3")]
	[InlineData("Marlin 1.0.0 (Github)")]
	public void Warns_about_marlin_before_1_1_0(string firmware)
	{
		HealthWarning w = Assert.Single(FirmwareCheck.Check(firmware));
		Assert.Equal(("firmware-thermal-runaway", HealthSeverity.Warning, $"Old firmware: {firmware}"), (w.Id, w.Severity, w.Title));
	}

	[Theory]
	[InlineData("Marlin 1.1.0")]
	[InlineData("Marlin 1.1.9.1")]
	[InlineData("Marlin 2.1.2 (PrintDeck Virtual)")]
	[InlineData("Marlin bugfix-2.1.x")]
	[InlineData("Prusa-Firmware 3.14.1 based on Marlin")]
	[InlineData("Klipper")]
	[InlineData("")]
	[InlineData(null)]
	public void Leaves_other_firmware_alone(string? firmware) => Assert.Empty(FirmwareCheck.Check(firmware));
}
