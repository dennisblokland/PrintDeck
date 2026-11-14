using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Application.Abstractions;

public sealed class PrinterEventHub : IPrinterEvents
{
	public event Action<PrinterChange>? Changed;

	public void Publish(PrinterChange change)
	{
		if (Changed is not { } handlers) return;
		foreach (Action<PrinterChange> handler in handlers.GetInvocationList().Cast<Action<PrinterChange>>())
		{
			try { handler(change); }
			catch { /* a broken subscriber must not break the printer loop */ }
		}
	}
}
