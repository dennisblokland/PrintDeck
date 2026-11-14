using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Domain.Tests.Temperatures;

public class HeaterReadingTests
{
	[Theory]
	[InlineData("B", "Bed")]
	[InlineData("C", "Chamber")]
	[InlineData("T", "Hotend")]
	[InlineData("T0", "Hotend")]
	[InlineData("T1", "Hotend 1")]
	[InlineData("X", "X")]
	public void Has_a_display_name(string heater, string expected) =>
		Assert.Equal(expected, new HeaterReading(heater, 0, 0).DisplayName);

	[Theory]
	[InlineData("B", true, false)]
	[InlineData("C", false, true)]
	[InlineData("T0", false, false)]
	public void Knows_bed_and_chamber(string heater, bool bed, bool chamber)
	{
		HeaterReading r = new(heater, 0, 0);
		Assert.Equal(bed, r.IsBed);
		Assert.Equal(chamber, r.IsChamber);
	}
}
