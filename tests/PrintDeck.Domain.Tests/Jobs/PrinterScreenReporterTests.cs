using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Domain.Tests.Jobs;

public class PrinterScreenReporterTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void Starts_at_zero_with_the_file_name()
	{
		Assert.Equal(["M73 P0", "M117 benchy 0.2mm PLA MK3"], new PrinterScreenReporter("benchy 0.2mm PLA MK3S.gcode", null).Start());
	}

	[Fact]
	public void Reports_percent_and_minutes_left_when_they_change()
	{
		PrinterScreenReporter screen = new("a.gcode", null);

		Assert.Equal("M73 P12 R46", screen.OnProgress(0.125, TimeSpan.FromMinutes(45.2), _t0));
		Assert.Null(screen.OnProgress(0.129, TimeSpan.FromMinutes(45.1), _t0.AddSeconds(30))); // same 12 % and 46 min
		Assert.Equal("M73 P13 R45", screen.OnProgress(0.13, TimeSpan.FromMinutes(44.5), _t0.AddSeconds(31)));
	}

	[Fact]
	public void Sends_at_most_every_five_seconds()
	{
		PrinterScreenReporter screen = new("a.gcode", null);
		Assert.NotNull(screen.OnProgress(0.10, null, _t0));
		Assert.Null(screen.OnProgress(0.20, null, _t0.AddSeconds(4)));
		Assert.Equal("M73 P20", screen.OnProgress(0.20, null, _t0.AddSeconds(5)));
	}

	[Fact]
	public void Follows_cura_and_orca_layer_numbers()
	{
		PrinterScreenReporter screen = new("a.gcode", 80);

		Assert.Equal("M117 Layer 1/80", screen.OnLine(";LAYER:0"));
		Assert.Null(screen.OnLine(";LAYER_CHANGE")); // explicit numbers win
		Assert.Null(screen.OnLine("G1 X1"));
		Assert.Equal("M117 Layer 2/80", screen.OnLine(";LAYER:1"));
		Assert.Equal(2, screen.Layer);
	}

	[Fact]
	public void Counts_prusaslicer_layer_changes()
	{
		PrinterScreenReporter screen = new("a.gcode", null);
		Assert.Equal("M117 Layer 1", screen.OnLine(";LAYER_CHANGE"));
		Assert.Equal("M117 Layer 2", screen.OnLine("; LAYER_CHANGE"));
	}

	[Fact]
	public void Steps_aside_when_the_file_sends_its_own_progress()
	{
		PrinterScreenReporter screen = new("a.gcode", null);

		screen.OnLine("M73 P0 R30 ; from the slicer");

		Assert.True(screen.FileReportsProgress);
		Assert.Null(screen.OnProgress(0.5, TimeSpan.FromMinutes(10), _t0));
		Assert.Equal(["M117 Print done"], screen.Finish(JobStatus.Completed));
	}

	[Fact]
	public void Says_how_the_print_ended()
	{
		PrinterScreenReporter screen = new("a.gcode", null);
		Assert.Equal(["M73 P100 R0", "M117 Print done"], screen.Finish(JobStatus.Completed));
		Assert.Equal(["M117 Print cancelled"], screen.Finish(JobStatus.Cancelled));
		Assert.Empty(screen.Finish(JobStatus.Failed));
	}

	[Fact]
	public void Paused_and_resumed_messages()
	{
		PrinterScreenReporter screen = new("a.gcode", 10);
		Assert.Equal(["M117 Paused"], screen.Paused());
		Assert.Equal(["M117 Printing"], screen.Resumed());
		screen.OnLine(";LAYER:4");
		Assert.Equal(["M117 Layer 5/10"], screen.Resumed());
	}

	[Theory]
	[InlineData("cube", "M117 cube")]
	[InlineData("a;b*c", "M117 abc")]
	[InlineData("tür", "M117 tr")]
	[InlineData("", "M117")]
	[InlineData("123456789012345678901234", "M117 12345678901234567890")]
	public void Messages_are_safe_and_fit_the_screen(string text, string expected) =>
		Assert.Equal(expected, PrinterScreenReporter.Message(text));
}
