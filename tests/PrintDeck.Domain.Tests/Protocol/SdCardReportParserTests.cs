using PrintDeck.Domain.Protocol;
using PrintDeck.Domain.SdCard;

namespace PrintDeck.Domain.Tests.Protocol;

public class SdCardReportParserTests
{
	[Fact]
	public void Reads_a_short_entry() =>
		Assert.Equal(new SdFile("CUBE.GCO", 1234), SdCardReportParser.TryParseFileEntry("CUBE.GCO 1234"));

	[Fact]
	public void Reads_a_long_name_with_spaces()
	{
		SdFile? file = SdCardReportParser.TryParseFileEntry("BENCHY~1.GCO 98765 benchy 0.2mm PLA.gcode");
		Assert.Equal(new SdFile("BENCHY~1.GCO", 98765, "benchy 0.2mm PLA.gcode"), file);
		Assert.Equal("benchy 0.2mm PLA.gcode", file!.DisplayName);
	}

	[Fact]
	public void Keeps_folders_in_the_path() =>
		Assert.Equal("PARTS/GEAR.GCO", SdCardReportParser.TryParseFileEntry("PARTS/GEAR.GCO 10")!.Path);

	[Fact]
	public void Works_without_a_size()
	{
		SdFile? file = SdCardReportParser.TryParseFileEntry("CUBE.G");
		Assert.Equal(0, file!.SizeBytes);
		Assert.Equal("CUBE.G", file.DisplayName);
	}

	[Theory]
	[InlineData("")]
	[InlineData("CONFIG.TXT 120")]
	[InlineData("FIRMWARE.BIN 200000")]
	[InlineData("PARTS/ 0")]
	public void Skips_what_is_not_gcode(string line) => Assert.Null(SdCardReportParser.TryParseFileEntry(line));

	[Fact]
	public void Reads_progress()
	{
		Assert.Equal((1234L, 56789L), SdCardReportParser.TryParseProgress("SD printing byte 1234/56789"));
		Assert.Equal((0L, 10L), SdCardReportParser.TryParseProgress("echo:SD printing byte 0/10"));
		Assert.Null(SdCardReportParser.TryParseProgress("Not SD printing"));
		Assert.Null(SdCardReportParser.TryParseProgress("SD printing byte x/10"));
	}

	[Theory]
	[InlineData("echo:SD card ok", true, false)]
	[InlineData("echo:SD init fail", false, true)]
	[InlineData("echo:No SD card", false, true)]
	[InlineData("echo:No media", false, true)]
	[InlineData("echo:SD card released", false, true)]
	[InlineData("ok", false, false)]
	public void Recognises_card_state(string line, bool ready, bool gone)
	{
		Assert.Equal(ready, SdCardReportParser.IsCardReady(line));
		Assert.Equal(gone, SdCardReportParser.IsCardGone(line));
	}
}
