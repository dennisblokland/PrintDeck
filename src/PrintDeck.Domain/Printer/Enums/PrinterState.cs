namespace PrintDeck.Domain.Printer.Enums;

public enum PrinterState
{
	Offline,
	Connecting,
	Operational,
	Printing,
	Paused,
	Cancelling,
	Error
}
