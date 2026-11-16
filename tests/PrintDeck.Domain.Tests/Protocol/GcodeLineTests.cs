using PrintDeck.Domain.Protocol;

namespace PrintDeck.Domain.Tests.Protocol;

public class GcodeLineTests
{
	[Theory]
	[InlineData("G1 X10 ; move", "G1 X10")]
	[InlineData("   ; just a comment", null)]
	[InlineData("", null)]
	[InlineData("   ", null)]
	[InlineData("  M104 S200  ", "M104 S200")]
	[InlineData("G28\t; home", "G28")]
	public void Clean_strips_comments_and_whitespace(string raw, string? expected) =>
		Assert.Equal(expected, GcodeLine.Clean(raw));

	[Fact]
	public void Checksum_is_xor_of_numbered_line()
	{
		string wire = GcodeLine.WithChecksum(1, "G28");
		byte cs = 0;
		foreach (char c in "N1 G28") cs ^= (byte) c;
		Assert.Equal($"N1 G28*{cs}", wire);
	}

	[Theory]
	[InlineData(1, "G28", "N1 G28*18")]
	[InlineData(0, "M110 N0", "N0 M110 N0*125")]
	public void Checksum_matches_known_values(long line, string command, string expected) =>
		Assert.Equal(expected, GcodeLine.WithChecksum(line, command));

	[Theory]
	[InlineData("G1 X10", "G1")]
	[InlineData("g1 x10", "G1")]
	[InlineData("  M104 S200", "M104")]
	[InlineData("G28*18", "G28")]
	[InlineData("M84", "M84")]
	[InlineData("", "")]
	public void CommandOf_returns_upper_case_command_word(string line, string expected) =>
		Assert.Equal(expected, GcodeLine.CommandOf(line));

	[Theory]
	[InlineData("M109 S210", true)]
	[InlineData("M190 S60", true)]
	[InlineData("M191 S40", true)]
	[InlineData("g28", true)]
	[InlineData("G29", true)]
	[InlineData("G4 P1000", true)]
	[InlineData("M400", true)]
	[InlineData("M303 E0 S200 C8", true)]
	[InlineData("M600", true)]
	[InlineData("M0 Remove the support", true)]
	[InlineData("M1", true)]
	[InlineData("M10", false)]
	[InlineData("G1 X1", false)]
	[InlineData("M104 S200", false)]
	[InlineData("G280", false)]
	public void Detects_long_running(string cmd, bool expected) => Assert.Equal(expected, GcodeLine.IsLongRunning(cmd));

	[Theory]
	[InlineData("M108", true)]
	[InlineData("M876 S0", true)]
	[InlineData("M112", true)]
	[InlineData("M410", true)]
	[InlineData("M87", false)]
	[InlineData("G28", false)]
	public void Detects_emergency_commands(string cmd, bool expected) => Assert.Equal(expected, GcodeLine.IsEmergency(cmd));
}
