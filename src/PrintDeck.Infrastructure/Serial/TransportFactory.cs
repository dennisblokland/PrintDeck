using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Serial;

public sealed class TransportFactory(TimeProvider time, IOptions<VirtualPrinterOptions>? virtualPrinter = null) : IPrinterTransportFactory
{
	public IPrinterTransport Create(string port, int baudRate) =>
		port == VirtualPrinterTransport.PortName
			? new VirtualPrinterTransport(time, firmwareName: FirmwareName)
			: new SerialTransport(port, baudRate);

	private string FirmwareName =>
		string.IsNullOrWhiteSpace(virtualPrinter?.Value.FirmwareName) ? VirtualPrinterOptions.DefaultFirmwareName : virtualPrinter.Value.FirmwareName.Trim();
}
