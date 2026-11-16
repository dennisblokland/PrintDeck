using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Terminal.Enums;

using static PrintDeck.Application.Tests.TestDoubles.Eventually;

namespace PrintDeck.Application.Tests.Printer;

public class PrinterSessionSerialLogTests : IAsyncDisposable
{
	private readonly ScriptedTransport _printer = new();
	private readonly FakeSerialLog _serialLog = new();
	private readonly PrinterSession _session;

	public PrinterSessionSerialLogTests()
	{
		_session = new PrinterSession(_printer, RecordingEventHub.Create().Hub, TimeProvider.System,
			NullLogger<PrinterSession>.Instance, serialLog: _serialLog);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Every_terminal_line_goes_to_the_serial_log_while_it_is_on()
	{
		_serialLog.SetEnabled(true);

		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		Assert.True(r.IsSuccess, r.Error);
		await Until(() => _serialLog.Written.Any(e => e.Text.Contains("M20 L", StringComparison.Ordinal)));

		Assert.Contains(_serialLog.Written, e => e.Direction == TerminalDirection.Info && e.Text.StartsWith("Connecting to SCRIPTED", StringComparison.Ordinal));
		Assert.Contains(_serialLog.Written, e => e.Direction == TerminalDirection.Sent && e.Text.Contains("M110 N0", StringComparison.Ordinal));
		Assert.Contains(_serialLog.Written, e => e.Direction == TerminalDirection.Received && e.Text == "ok");
	}

	[Fact]
	public async Task Nothing_is_logged_while_it_is_off()
	{
		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		Assert.True(r.IsSuccess, r.Error);
		Assert.Empty(_serialLog.Written);
	}
}
