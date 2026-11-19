using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Application.Abstractions.Enums;

[Flags]
public enum PrinterChange
{
	None = 0,
	State = 1,
	Temperature = 2,
	Terminal = 4,
	Job = 8,
	Files = 16,
	Settings = 32,

	/// <summary>Firmware prompt, notification or wait-for-user changed.</summary>
	Dialog = 64,

	/// <summary>A streamed print reached a new layer (<see cref="Printer.Interfaces.IPrinterSession.Layer"/>).</summary>
	Layer = 128,

	/// <summary>A timelapse started or stopped recording, took a frame, or a video was rendered or deleted.</summary>
	Timelapse = 256,
	All = State | Temperature | Terminal | Job | Files | Settings | Dialog | Layer | Timelapse
}
