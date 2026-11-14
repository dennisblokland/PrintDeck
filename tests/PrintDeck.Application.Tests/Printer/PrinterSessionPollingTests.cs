using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;

using static PrintDeck.Application.Tests.TestDoubles.Eventually;

namespace PrintDeck.Application.Tests.Printer;

/// <summary>M105 fallback for firmware without temperature auto-reporting. Real time: the poll runs every 2 s.</summary>
public sealed class PrinterSessionPollingTests : IAsyncDisposable
{
	private readonly ScriptedTransport _printer = new();
	private readonly PrinterSession _session;

	public PrinterSessionPollingTests()
	{
		_session = new PrinterSession(_printer, new PrinterEventHub(), TimeProvider.System, NullLogger<PrinterSession>.Instance);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	[Fact]
	public async Task Polls_with_M105_when_the_printer_does_not_report()
	{
		_printer.Respond = line => ScriptedTransport.CommandOf(line) == "M105"
			? ["ok T:24.5 /0.0 B:23.0 /0.0 @:0 B@:0"]
			: ["ok"];

		Result r = await _session.ConnectAsync("SCRIPTED", 115200, TestContext.Current.CancellationToken);
		Assert.True(r.IsSuccess, r.Error);

		await Until(() => _session.Temperatures.Latest is not null, seconds: 8);
		Assert.Contains("M105", _printer.Commands);
		Assert.Equal(24.5, _session.Temperatures.Latest!.Heaters.Single(h => h.Heater == "T0").Actual);
	}
}
