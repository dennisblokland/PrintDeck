using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Application.Features.Terminal.Handlers;
using PrintDeck.Application.Features.Terminal.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Tests.Features.Terminal;

public class TerminalHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private void Log(TerminalDirection direction, string text) =>
		_session.TerminalEntries.Add(new TerminalEntry(_session.TerminalEntries.Count + 1, DateTimeOffset.UnixEpoch, direction, text));

	private async Task<IReadOnlyList<string>> Read(int last, bool hideTemperature) =>
		(await new GetTerminalLogHandler(_session).Handle(new GetTerminalLogQuery(last, hideTemperature), Ct)).Select(e => e.Text).ToList();

	public TerminalHandlerTests()
	{
		Log(TerminalDirection.Sent, "N1 G28*18");
		Log(TerminalDirection.Received, "ok");
		Log(TerminalDirection.Received, " T:200.0 /200.0 B:60.0 /60.0");
		Log(TerminalDirection.Sent, "N2 M105*37");
		Log(TerminalDirection.Sent, "ok");
		Log(TerminalDirection.Received, "echo:busy: processing");
	}

	[Fact]
	public async Task Hides_temperature_reports_polls_and_bare_oks()
	{
		Assert.Equal(["N1 G28*18", "ok", "echo:busy: processing"], await Read(10, hideTemperature: true));
	}

	[Fact]
	public async Task Shows_everything_when_not_filtering()
	{
		Assert.Equal(6, (await Read(10, hideTemperature: false)).Count);
	}

	[Fact]
	public async Task Returns_only_the_last_entries()
	{
		Assert.Equal(["ok", "echo:busy: processing"], await Read(2, hideTemperature: true));
		Assert.Equal(["ok", "echo:busy: processing"], await Read(2, hideTemperature: false));
	}

	[Fact]
	public async Task Clear_empties_the_session_log()
	{
		Assert.True((await new ClearTerminalHandler(_session).Handle(new ClearTerminalCommand(), Ct)).IsSuccess);
		Assert.True(_session.TerminalCleared);
	}
}
