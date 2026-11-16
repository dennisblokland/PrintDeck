namespace PrintDeck.Domain.Printer.Enums;

public enum PrinterState
{
	Offline,
	Connecting,
	Operational,
	Printing,
	Paused,
	Cancelling,

	/// <summary>Copying a file onto the SD card.</summary>
	Transferring,

	Error
}
