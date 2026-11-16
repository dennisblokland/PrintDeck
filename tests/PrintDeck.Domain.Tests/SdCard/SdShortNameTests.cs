using PrintDeck.Domain.SdCard;

namespace PrintDeck.Domain.Tests.SdCard;

public class SdShortNameTests
{
	[Theory]
	[InlineData("cube.gcode", "CUBE.GCO")]
	[InlineData("Benchy 0.2mm PLA.gcode", "BENCHY02.GCO")]
	[InlineData("my_part-v2.gco", "MY_PART-.GCO")]
	[InlineData("ü.gcode", "FILE.GCO")]
	public void Makes_an_8_3_name(string fileName, string expected) =>
		Assert.Equal(expected, SdShortName.For(fileName, []));

	[Fact]
	public void Numbers_names_that_are_taken()
	{
		Assert.Equal("BENCHY~1.GCO", SdShortName.For("benchy.gcode", ["BENCHY.GCO"]));
		Assert.Equal("BENCHY~2.GCO", SdShortName.For("benchy.gcode", ["benchy.gco", "BENCHY~1.GCO"]));
		Assert.Equal("CUBE~1.GCO", SdShortName.For("cube.gcode", ["CUBE.GCO"]));
	}

	[Fact]
	public void Keeps_numbered_names_within_eight_characters()
	{
		string[] taken = ["LONGNAME.GCO", .. Enumerable.Range(1, 9).Select(n => $"LONGNA~{n}.GCO")];
		Assert.Equal("LONGN~10.GCO", SdShortName.For("longname.gcode", taken));
	}
}
