using PrintDeck.Domain.Protocol;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Domain.Tests.Protocol;

public class TemperatureReportParserTests
{
	[Fact]
	public void Parses_single_extruder_ok_report()
	{
		IReadOnlyList<HeaterReading>? r = TemperatureReportParser.TryParse("ok T:210.3 /210.0 B:59.8 /60.0 @:64 B@:0");
		Assert.NotNull(r);
		Assert.Collection(r,
			t => { Assert.Equal("T0", t.Heater); Assert.Equal(210.3, t.Actual); Assert.Equal(210, t.Target); },
			b => { Assert.Equal("B", b.Heater); Assert.Equal(59.8, b.Actual); Assert.Equal(60, b.Target); });
	}

	[Fact]
	public void Prefers_T0_over_T_on_multi_extruder()
	{
		IReadOnlyList<HeaterReading>? r = TemperatureReportParser.TryParse(" T:200.0 /200.0 B:60.0 /60.0 T0:200.0 /200.0 T1:25.0 /0.0 @:0");
		Assert.Equal(["B", "T0", "T1"], r!.Select(h => h.Heater).Order());
	}

	[Fact]
	public void Reads_the_chamber_and_lists_the_bed_last()
	{
		IReadOnlyList<HeaterReading>? r = TemperatureReportParser.TryParse("T:25.0 /0.0 B:22.0 /0.0 C:30.5 /40.0");
		Assert.Equal(["C", "T0", "B"], r!.Select(h => h.Heater));
		Assert.Equal(40, r!.Single(h => h.Heater == "C").Target);
	}

	[Fact]
	public void Missing_target_reads_as_zero()
	{
		HeaterReading t = Assert.Single(TemperatureReportParser.TryParse("T:20.5")!);
		Assert.Equal(20.5, t.Actual);
		Assert.Equal(0, t.Target);
	}

	[Fact]
	public void Reads_negative_temperatures()
	{
		HeaterReading t = Assert.Single(TemperatureReportParser.TryParse("T:-14.0 /0.0")!);
		Assert.Equal(-14, t.Actual);
	}

	[Fact]
	public void Reads_bed_only_reports()
	{
		HeaterReading b = Assert.Single(TemperatureReportParser.TryParse("ok B:59.0 /60.0")!);
		Assert.True(b.IsBed);
	}

	[Theory]
	[InlineData("ok")]
	[InlineData("echo:busy: processing")]
	[InlineData("X:0.00 Y:0.00 Z:0.00 E:0.00 Count X:0 Y:0 Z:0")]
	[InlineData("FIRMWARE_NAME:Marlin 2.1.2")]
	public void Ignores_non_temperature_lines(string line) => Assert.Null(TemperatureReportParser.TryParse(line));
}
