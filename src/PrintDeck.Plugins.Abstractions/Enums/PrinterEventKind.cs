namespace PrintDeck.Plugins.Enums;

public enum PrinterEventKind
{
	Connected,
	Disconnected,

	/// <summary>The printer reported an error, or the connection failed.</summary>
	Error,

	/// <summary>A print started streaming or from the SD card. Copying a file onto the card is not a print.</summary>
	PrintStarted,
	PrintPaused,
	PrintResumed,
	PrintDone,
	PrintFailed,
	PrintCancelled,

	/// <summary>A timelapse started recording.</summary>
	TimelapseStarted,

	/// <summary>A timelapse took its last frame and finished rendering (or was thrown away).</summary>
	TimelapseStopped,

	/// <summary>Any setting changed, the module's own included.</summary>
	SettingsChanged
}
