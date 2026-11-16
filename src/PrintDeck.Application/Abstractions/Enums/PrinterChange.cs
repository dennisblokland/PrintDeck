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
	All = State | Temperature | Terminal | Job | Files | Settings | Dialog
}
