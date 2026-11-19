namespace PrintDeck.Domain.Timelapse.Enums;

/// <summary>When PrintDeck takes a timelapse frame during a print.</summary>
public enum TimelapseMode
{
	/// <summary>No timelapse.</summary>
	Off,

	/// <summary>One frame every few seconds while the print runs (not while it is paused).</summary>
	Timed,

	/// <summary>One frame when each new layer starts. SD card prints, which PrintDeck can't read along with, fall back to <see cref="Timed"/>.</summary>
	Layer
}
