using PrintDeck.Application.Abstractions.Enums;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Push channel for the UI: says *what* changed; components re-query via the mediator.</summary>
public interface IPrinterEvents
{
	event Action<PrinterChange>? Changed;
}
