using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Serial;

public sealed class TransportFactory(TimeProvider time) : IPrinterTransportFactory
{
	public IPrinterTransport Create(string port, int baudRate) =>
		port == VirtualPrinterTransport.PortName
			? new VirtualPrinterTransport(time)
			: new SerialTransport(port, baudRate);
}
