using PrintDeck.Domain.Printer.Enums;

namespace PrintDeck.Domain.Printer;

public static class PrinterStateExtensions
{
	public static bool IsConnected(this PrinterState s) =>
		s is PrinterState.Operational or PrinterState.Printing or PrinterState.Paused or PrinterState.Cancelling;

	public static bool IsBusy(this PrinterState s) =>
		s is PrinterState.Printing or PrinterState.Paused or PrinterState.Cancelling;
}
