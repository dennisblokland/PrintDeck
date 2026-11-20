namespace PrintDeck.Plugins.Enums;

/// <summary>What the printer is doing, as a module sees it.</summary>
public enum PrinterActivity
{
	Offline,
	Connecting,
	Idle,
	Printing,
	Paused,
	Cancelling,

	/// <summary>Copying a file onto the SD card.</summary>
	Transferring,
	Error
}
