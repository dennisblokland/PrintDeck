using PrintDeck.Domain.Terminal;

namespace PrintDeck.Domain.Tests.Terminal;

public class TerminalFilterTests
{
	private static TerminalFilter Default(string name) => TerminalFilter.Defaults.Single(f => f.Name == name);

	[Theory]
	[InlineData("ok T:210.0 /210.0 B:60.0 /60.0 @:0 B@:0", true)]
	[InlineData(" T:210.0 /210.0 B:60.0 /60.0", true)]
	[InlineData("T:210.0 /210.0", true)]
	[InlineData("T0:210.0 /210.0 T1:25.0 /0.0", true)]
	[InlineData("M105", true)]
	[InlineData("m105", true)]
	[InlineData("N12 M105*39", true)]
	[InlineData("N0 M105 *7", true)]
	[InlineData("N12 M1050*3", false)]
	[InlineData("N3 G28*18", false)]
	[InlineData("N12", false)]
	[InlineData("ok", false)]
	[InlineData("echo:busy: processing", false)]
	[InlineData("N12 G1 X10*85", false)]
	[InlineData("Error: T:0 heating failed", false)]
	public void Temperature_filter_matches_reports_and_polls(string text, bool expected) =>
		Assert.Equal(expected, Default("Temperature reports").Matches(text));

	[Theory]
	[InlineData("ok", true)]
	[InlineData("OK ", true)]
	[InlineData("ok T:210.0", false)]
	[InlineData("ok, done", false)]
	public void Bare_ok_filter_matches_only_a_lone_ok(string text, bool expected) =>
		Assert.Equal(expected, Default("Bare ok").Matches(text));

	[Theory]
	[InlineData("SD status", "N40 M27*20", true)]
	[InlineData("SD status", "SD printing byte 1200/50000", true)]
	[InlineData("SD status", "Not SD printing", true)]
	[InlineData("SD status", "N40 M270*20", false)]
	[InlineData("Position reports", "N5 M114*33", true)]
	[InlineData("Position reports", "X:10.00 Y:20.00 Z:0.20 E:0.00 Count X:800 Y:1600 Z:80", true)]
	[InlineData("Position reports", "X:-1.00 Y:0.00 Z:0.00 E:0.00", true)]
	[InlineData("Busy", "echo:busy: processing", true)]
	[InlineData("Busy", "busy: paused for user", true)]
	[InlineData("Wait", "wait", true)]
	[InlineData("Wait", "echo:wait for user", false)]
	public void Other_default_filters_match_their_lines(string name, string text, bool expected) =>
		Assert.Equal(expected, Default(name).Matches(text));

	[Fact]
	public void Only_temperature_and_bare_ok_are_on_by_default()
	{
		Assert.Equal(["Temperature reports", "Bare ok"], TerminalFilter.Defaults.Where(f => f.Enabled).Select(f => f.Name));
		Assert.All(TerminalFilter.Defaults, f => Assert.True(TerminalFilter.IsValidPattern(f.Pattern), f.Name));
	}

	[Fact]
	public void Matching_ignores_case()
	{
		Assert.True(new TerminalFilter("Echo", "^echo:").Matches("ECHO:Unknown command"));
	}

	[Theory]
	[InlineData("^echo:", true)]
	[InlineData("busy|wait", true)]
	[InlineData("(unclosed", false)]
	[InlineData("[z-a]", false)]
	[InlineData("", false)]
	[InlineData("  ", false)]
	public void Validates_patterns(string pattern, bool valid) =>
		Assert.Equal(valid, TerminalFilter.IsValidPattern(pattern));

	[Fact]
	public void An_invalid_or_empty_pattern_hides_nothing()
	{
		Assert.False(new TerminalFilter("Broken", "(unclosed").Matches("(unclosed"));
		Assert.False(new TerminalFilter("Empty", "").Matches("ok"));
	}
}
