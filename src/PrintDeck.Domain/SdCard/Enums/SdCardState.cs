namespace PrintDeck.Domain.SdCard.Enums;

public enum SdCardState
{
	/// <summary>Not asked yet, or the printer is offline.</summary>
	Unknown,

	/// <summary>Card initialised (<c>SD card ok</c>); files can be listed and printed.</summary>
	Ready,

	/// <summary>No card, init failed, or released with <c>M22</c>.</summary>
	Unavailable
}
