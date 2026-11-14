using PrintDeck.Infrastructure.Serial;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Serial;

public class PortDiscoveryTests
{
	[Fact]
	public void Always_offers_the_virtual_printer_last()
	{
		Assert.Equal(VirtualPrinterTransport.PortName, new PortDiscovery().ListPorts()[^1]);
	}

	[Fact]
	public void Hides_plain_serial_ttys()
	{
		Assert.DoesNotContain(new PortDiscovery().ListPorts(), p => p.StartsWith("/dev/ttyS", StringComparison.Ordinal));
	}

	[Fact]
	public void Lists_each_port_once()
	{
		IReadOnlyList<string> ports = new PortDiscovery().ListPorts();
		Assert.Equal(ports.Count, ports.Distinct().Count());
	}
}
