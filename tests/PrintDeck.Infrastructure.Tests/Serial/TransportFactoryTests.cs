using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Infrastructure.Serial;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Serial;

public class TransportFactoryTests
{
	private readonly TransportFactory _factory = new(TimeProvider.System);

	[Fact]
	public async Task Virtual_port_gets_the_virtual_printer()
	{
		await using IPrinterTransport t = _factory.Create(VirtualPrinterTransport.PortName, 115200);
		Assert.IsType<VirtualPrinterTransport>(t);
	}

	[Fact]
	public async Task Other_ports_get_a_serial_transport()
	{
		await using IPrinterTransport t = _factory.Create("/dev/ttyUSB99", 250000);
		Assert.IsType<SerialTransport>(t);
	}
}
