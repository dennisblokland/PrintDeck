using System.IO.Ports;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Serial;

public sealed class PortDiscovery : IPortDiscovery
{
	private static readonly string[] _linuxPatterns = ["ttyUSB*", "ttyACM*", "ttyAMA*", "rfcomm*"];

	public IReadOnlyList<string> ListPorts()
	{
		SortedSet<string> ports = new(StringComparer.Ordinal);
		try { foreach (string? p in SerialPort.GetPortNames()) ports.Add(p); } catch { /* no serial subsystem */ }

		if (OperatingSystem.IsLinux())
		{
			foreach (string pattern in _linuxPatterns)
				foreach (string dev in Directory.EnumerateFiles("/dev", pattern)) ports.Add(dev);
			if (Directory.Exists("/dev/serial/by-id"))
				foreach (string dev in Directory.EnumerateFileSystemEntries("/dev/serial/by-id")) ports.Add(dev);
		}

		// Plain /dev/ttyS* are almost never printers; hide them unless nothing else exists.
		List<string> useful = ports.Where(p => !p.StartsWith("/dev/ttyS", StringComparison.Ordinal)).ToList();
		return [.. useful, VirtualPrinterTransport.PortName];
	}
}
