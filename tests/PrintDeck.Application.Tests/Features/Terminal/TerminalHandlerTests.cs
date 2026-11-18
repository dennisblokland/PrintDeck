using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Application.Features.Terminal.Handlers;
using PrintDeck.Application.Features.Terminal.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Tests.Features.Terminal;

public class TerminalHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private readonly FakeSettingsStore _settings = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private void Log(TerminalDirection direction, string text) =>
		_session.TerminalEntries.Add(new TerminalEntry(_session.TerminalEntries.Count + 1, DateTimeOffset.UnixEpoch, direction, text));

	private async Task<IReadOnlyList<string>> Read(int last, bool filtered) =>
		(await new GetTerminalLogHandler(_session, _settings).Handle(new GetTerminalLogQuery(last, filtered), Ct)).Select(e => e.Text).ToList();

	private Task UseFilters(params TerminalFilter[] filters) =>
		_settings.UpdateAsync(s => s with { TerminalFilters = filters }, Ct);

	public TerminalHandlerTests()
	{
		Log(TerminalDirection.Sent, "N1 G28*18");
		Log(TerminalDirection.Received, "ok");
		Log(TerminalDirection.Received, " T:200.0 /200.0 B:60.0 /60.0");
		Log(TerminalDirection.Sent, "N2 M105*37");
		Log(TerminalDirection.Received, "ok");
		Log(TerminalDirection.Received, "echo:busy: processing");
	}

	[Fact]
	public async Task Default_filters_hide_temperature_reports_polls_and_bare_oks()
	{
		Assert.Equal(["N1 G28*18", "echo:busy: processing"], await Read(10, filtered: true));
	}

	[Fact]
	public async Task Shows_everything_when_not_filtering()
	{
		Assert.Equal(6, (await Read(10, filtered: false)).Count);
	}

	[Fact]
	public async Task Returns_only_the_last_entries()
	{
		Assert.Equal(["N1 G28*18", "echo:busy: processing"], await Read(2, filtered: true));
		Assert.Equal(["ok", "echo:busy: processing"], await Read(2, filtered: false));
	}

	[Fact]
	public async Task Applies_the_saved_filters_that_are_on()
	{
		await UseFilters(new TerminalFilter("Busy", "busy:"), new TerminalFilter("Temperature", "T:", Enabled: false));

		Assert.Equal(["N1 G28*18", "ok", " T:200.0 /200.0 B:60.0 /60.0", "N2 M105*37", "ok"], await Read(10, filtered: true));
	}

	[Fact]
	public async Task Shows_everything_when_every_filter_is_off()
	{
		await UseFilters(TerminalFilter.Defaults.Select(f => f with { Enabled = false }).ToArray());

		Assert.Equal(6, (await Read(10, filtered: true)).Count);
	}

	[Fact]
	public async Task Clear_empties_the_session_log()
	{
		Assert.True((await new ClearTerminalHandler(_session).Handle(new ClearTerminalCommand(), Ct)).IsSuccess);
		Assert.True(_session.TerminalCleared);
	}
}
