using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Domain.Tests.Terminal;

public class TerminalEntryTests
{
	[Theory]
	[InlineData("ok T:210.0 /210.0 B:60.0 /60.0 @:0 B@:0", true)]
	[InlineData(" T:210.0 /210.0 B:60.0 /60.0", true)]
	[InlineData("T:210.0 /210.0", true)]
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
	public void Detects_temperature_chatter(string text, bool expected) =>
		Assert.Equal(expected, new TerminalEntry(1, DateTimeOffset.UnixEpoch, TerminalDirection.Received, text).IsTemperatureChatter);
}
