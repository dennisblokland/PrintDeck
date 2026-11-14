using PrintDeck.Application.Abstractions.Enums;

namespace PrintDeck.Application.Abstractions.Interfaces;

public interface IPortDiscovery
{
	IReadOnlyList<string> ListPorts();
}
