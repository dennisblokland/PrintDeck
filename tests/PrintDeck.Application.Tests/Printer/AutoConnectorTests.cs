using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Printer;

public class AutoConnectorTests
{
	private const string Port = "/dev/ttyACM0";

	private readonly FakePrinterSession _session = new() { State = PrinterState.Offline };
	private readonly FakeSettingsStore _settings = new(new HostSettings { ApiKey = "KEY", AutoConnect = true, LastPort = Port, DefaultBaudRate = 250000 });
	private readonly StubPorts _ports = new() { Present = [Port, "VIRTUAL"] };
	private readonly AutoConnector _connector;

	public AutoConnectorTests() => _connector = new AutoConnector(_session, _settings, _ports, NullLogger<AutoConnector>.Instance);

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private Task<bool> Check() => _connector.CheckAsync(Ct);

	[Fact]
	public async Task Connects_to_the_last_port_and_baud_rate_on_the_first_check()
	{
		Assert.True(await Check());
		Assert.Equal((Port, 250000), _session.ConnectedWith);
	}

	[Fact]
	public async Task Does_nothing_when_switched_off()
	{
		await _settings.UpdateAsync(s => s with { AutoConnect = false }, Ct);

		Assert.False(await Check());
		Assert.Equal(0, _session.ConnectCalls);
	}

	[Fact]
	public async Task Does_nothing_without_a_last_port()
	{
		await _settings.UpdateAsync(s => s with { LastPort = null }, Ct);

		Assert.False(await Check());
		Assert.Equal(0, _session.ConnectCalls);
	}

	[Fact]
	public async Task Waits_for_the_port_to_appear()
	{
		_ports.Present = ["VIRTUAL"];
		Assert.False(await Check());
		Assert.False(await Check());

		_ports.Present = [Port, "VIRTUAL"];
		Assert.True(await Check());
		Assert.Equal(1, _session.ConnectCalls);
	}

	[Fact]
	public async Task Does_not_retry_a_failed_attempt_until_the_port_comes_back()
	{
		_session.ConnectResult = Result.Failure("No response from printer (check port and baud rate).");
		Assert.True(await Check());
		_session.State = PrinterState.Error;

		Assert.False(await Check());
		Assert.False(await Check());
		Assert.Equal(1, _session.ConnectCalls);

		_ports.Present = ["VIRTUAL"];
		Assert.False(await Check());
		_ports.Present = [Port, "VIRTUAL"];
		Assert.True(await Check());
		Assert.Equal(2, _session.ConnectCalls);
	}

	[Fact]
	public async Task Reconnects_after_the_printer_is_switched_off_and_on()
	{
		Assert.True(await Check());
		_session.State = PrinterState.Operational;
		Assert.False(await Check());

		// Switched off: the connection is lost and the USB port disappears.
		_session.State = PrinterState.Error;
		_ports.Present = ["VIRTUAL"];
		Assert.False(await Check());

		_ports.Present = [Port, "VIRTUAL"];
		Assert.True(await Check());
		Assert.Equal(2, _session.ConnectCalls);
	}

	[Theory]
	[InlineData(PrinterState.Connecting)]
	[InlineData(PrinterState.Operational)]
	[InlineData(PrinterState.Printing)]
	public async Task Leaves_a_session_that_is_already_connecting_or_connected(PrinterState state)
	{
		_session.State = state;

		Assert.False(await Check());
		Assert.Equal(0, _session.ConnectCalls);
	}

	[Fact]
	public async Task Stays_offline_after_a_manual_disconnect_while_the_port_is_still_there()
	{
		Assert.True(await Check());
		_session.State = PrinterState.Operational;
		Assert.False(await Check());

		_session.State = PrinterState.Offline; // Disconnect button
		Assert.False(await Check());
		Assert.Equal(1, _session.ConnectCalls);
	}

	[Fact]
	public async Task Connects_when_switched_on_while_the_port_is_there()
	{
		await _settings.UpdateAsync(s => s with { AutoConnect = false }, Ct);
		Assert.False(await Check());

		await _settings.UpdateAsync(s => s with { AutoConnect = true }, Ct);
		Assert.True(await Check());
	}

	[Fact]
	public async Task Follows_a_change_of_last_port()
	{
		Assert.True(await Check());
		_session.State = PrinterState.Error;

		await _settings.UpdateAsync(s => s with { LastPort = "VIRTUAL" }, Ct);
		Assert.True(await Check());
		Assert.Equal(("VIRTUAL", 250000), _session.ConnectedWith);
	}

	private sealed class StubPorts : IPortDiscovery
	{
		public IReadOnlyList<string> Present { get; set; } = [];
		public IReadOnlyList<string> ListPorts() => Present;
	}
}
