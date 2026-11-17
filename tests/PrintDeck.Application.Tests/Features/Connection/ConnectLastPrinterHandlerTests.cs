using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Features.Connection.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Connection;

public class ConnectLastPrinterHandlerTests
{
	private readonly FakePrinterSession _session = new() { State = PrinterState.Offline };
	private readonly FakeSettingsStore _settings = new(new HostSettings { ApiKey = "KEY", LastPort = "/dev/ttyACM0", DefaultBaudRate = 250000 });

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Connect() =>
		new ConnectLastPrinterHandler(_session, _settings).Handle(new ConnectLastPrinterCommand(), Ct);

	[Theory]
	[InlineData(PrinterState.Operational)]
	[InlineData(PrinterState.Printing)]
	public async Task Leaves_a_connected_printer_alone(PrinterState state)
	{
		_session.State = state;

		Assert.True((await Connect()).IsSuccess);
		Assert.Equal(0, _session.ConnectCalls);
	}

	[Theory]
	[InlineData(PrinterState.Offline)]
	[InlineData(PrinterState.Error)]
	public async Task Connects_to_the_last_port_and_baud_rate(PrinterState state)
	{
		_session.State = state;

		Assert.True((await Connect()).IsSuccess);
		Assert.Equal(("/dev/ttyACM0", 250000), _session.ConnectedWith);
	}

	[Fact]
	public async Task Passes_on_a_failed_connect()
	{
		_session.ConnectResult = Result.Failure("No response from printer (check port and baud rate).");

		Assert.Equal("No response from printer (check port and baud rate).", (await Connect()).Error);
	}

	[Fact]
	public async Task Needs_a_last_port()
	{
		await _settings.UpdateAsync(s => s with { LastPort = null }, Ct);

		Result r = await Connect();

		Assert.Equal("Printer is not connected, and there is no last port to connect to. Connect once from PrintDeck first.", r.Error);
		Assert.Equal(0, _session.ConnectCalls);
	}

	[Fact]
	public async Task Waits_for_a_connect_that_is_under_way()
	{
		_session.State = PrinterState.Connecting;
		ValueTask<Result> pending = Connect();

		await Task.Delay(250, Ct);
		Assert.False(pending.IsCompleted);
		_session.State = PrinterState.Operational;

		Assert.True((await pending).IsSuccess);
		Assert.Equal(0, _session.ConnectCalls);
	}

	[Fact]
	public async Task Tries_again_when_the_connect_under_way_fails()
	{
		_session.State = PrinterState.Connecting;
		ValueTask<Result> pending = Connect();

		await Task.Delay(250, Ct);
		_session.State = PrinterState.Error;

		Assert.True((await pending).IsSuccess);
		Assert.Equal(1, _session.ConnectCalls);
	}
}
