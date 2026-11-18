using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Printer;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Health;
using PrintDeck.Infrastructure.Serial;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Virtual;

/// <summary>The virtual printer can pretend to run other firmware, so the firmware health check can be tried.</summary>
public sealed class VirtualPrinterFirmwareNameTests : IAsyncDisposable
{
	private readonly PrinterSession _session = new(
		new TransportFactory(TimeProvider.System, Options.Create(new VirtualPrinterOptions { FirmwareName = "Marlin 1.0.2" })),
		new PrinterEventHub(), TimeProvider.System, NullLogger<PrinterSession>.Instance);

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	[Fact]
	public async Task Reports_the_configured_firmware_name()
	{
		Result connected = await _session.ConnectAsync(VirtualPrinterTransport.PortName, 115200, Ct);
		Assert.True(connected.IsSuccess, connected.Error);

		DateTime deadline = DateTime.UtcNow.AddSeconds(10);
		while (_session.Firmware is null && DateTime.UtcNow < deadline) await Task.Delay(50, Ct);

		Assert.Equal("Marlin 1.0.2", _session.Firmware);
		Assert.Single(FirmwareCheck.Check(_session.Firmware));
	}
}
