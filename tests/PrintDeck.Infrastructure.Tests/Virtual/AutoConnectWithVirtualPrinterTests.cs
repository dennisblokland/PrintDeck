using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;
using PrintDeck.Infrastructure.Serial;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Virtual;

/// <summary>End-to-end: auto-connect brings the real session up on the virtual printer.</summary>
public sealed class AutoConnectWithVirtualPrinterTests : IAsyncDisposable
{
	private readonly PrinterSession _session = new(
		new TransportFactory(TimeProvider.System), new PrinterEventHub(), TimeProvider.System, NullLogger<PrinterSession>.Instance);

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	[Fact]
	public async Task Connects_to_the_virtual_printer_on_start()
	{
		HostSettings settings = new() { AutoConnect = true, LastPort = VirtualPrinterTransport.PortName, DefaultBaudRate = 115200 };
		using AutoConnector connector = new(_session, new FixedSettings(settings), new PortDiscovery(), NullLogger<AutoConnector>.Instance);

		connector.Start();

		DateTime deadline = DateTime.UtcNow.AddSeconds(15);
		while (_session.State != PrinterState.Operational)
		{
			if (DateTime.UtcNow > deadline) throw new TimeoutException($"Still {_session.State}.");
			await Task.Delay(50, Ct);
		}
		Assert.Equal(VirtualPrinterTransport.PortName, _session.Port);
		Assert.Equal(115200, _session.BaudRate);
	}

	private sealed class FixedSettings(HostSettings settings) : ISettingsStore
	{
		public Task<HostSettings> GetAsync(CancellationToken ct) => Task.FromResult(settings);
		public Task<HostSettings> UpdateAsync(Func<HostSettings, HostSettings> change, CancellationToken ct) => Task.FromResult(change(settings));
	}
}
