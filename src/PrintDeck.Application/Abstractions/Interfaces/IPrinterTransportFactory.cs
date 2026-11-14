using PrintDeck.Application.Abstractions.Enums;

namespace PrintDeck.Application.Abstractions.Interfaces;

public interface IPrinterTransportFactory
{
	IPrinterTransport Create(string port, int baudRate);
}
