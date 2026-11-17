using PrintDeck.Domain.Protocol;

namespace PrintDeck.Domain.Tests.Protocol;

public class RateReportParserTests
{
	[Theory]
	[InlineData("FR:100%", 100)]
	[InlineData("echo:FR:85%", 85)]
	[InlineData("FR: 150 %", 150)]
	[InlineData("ok", null)]
	[InlineData("T:200.0 /200.0 FR:1", null)]
	public void Reads_feed_rate_reports(string line, int? expected) => Assert.Equal(expected, RateReportParser.TryParseFeedReport(line));

	[Theory]
	[InlineData("echo:E0 Flow: 95%", 95)]
	[InlineData("E0 Flow: 100%", 100)]
	[InlineData("E0 extrude factor: 110%", 110)]
	[InlineData("echo:Flow: 95%", null)]
	[InlineData("ok", null)]
	public void Reads_flow_reports(string line, int? expected) => Assert.Equal(expected, RateReportParser.TryParseFlowReport(line));

	[Theory]
	[InlineData("M220 S120", 120)]
	[InlineData("m220 s90", 90)]
	[InlineData("M220 S99.6", 100)]
	[InlineData("M220", null)]
	[InlineData("M220 B", null)]
	[InlineData("M221 S95", null)]
	[InlineData("G1 X10 S5", null)]
	public void Reads_the_feed_rate_a_command_sets(string command, int? expected) => Assert.Equal(expected, RateReportParser.FeedSetBy(command));

	[Theory]
	[InlineData("M221 S95", 95)]
	[InlineData("M221 T0 S105", 105)]
	[InlineData("M221", null)]
	[InlineData("M220 S95", null)]
	public void Reads_the_flow_a_command_sets(string command, int? expected) => Assert.Equal(expected, RateReportParser.FlowSetBy(command));
}
