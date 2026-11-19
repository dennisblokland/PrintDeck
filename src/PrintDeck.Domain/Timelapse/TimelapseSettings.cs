using PrintDeck.Domain.Timelapse.Enums;

namespace PrintDeck.Domain.Timelapse;

/// <summary>How timelapses are taken and rendered.</summary>
/// <param name="Mode">Off, a frame every <paramref name="IntervalSeconds"/>, or a frame per layer.</param>
/// <param name="IntervalSeconds">Seconds between frames in <see cref="TimelapseMode.Timed"/> mode.</param>
/// <param name="FrameRate">Frames per second of the rendered video.</param>
public sealed record TimelapseSettings(TimelapseMode Mode = TimelapseMode.Off, int IntervalSeconds = 10, int FrameRate = 25)
{
	public const int MinInterval = 1;
	public const int MaxInterval = 3600;
	public const int MinFrameRate = 1;
	public const int MaxFrameRate = 60;

	/// <summary>A short reason when a value is out of range, otherwise null.</summary>
	public string? Problem() =>
		!Enum.IsDefined(Mode) ? "Pick a timelapse mode."
		: IntervalSeconds is < MinInterval or > MaxInterval ? $"The timelapse interval must be {MinInterval}–{MaxInterval} seconds."
		: FrameRate is < MinFrameRate or > MaxFrameRate ? $"The timelapse frame rate must be {MinFrameRate}–{MaxFrameRate} frames per second."
		: null;

	/// <summary>Repairs a hand-edited file: unknown mode off, numbers into range.</summary>
	public TimelapseSettings Normalized() => new(
		Enum.IsDefined(Mode) ? Mode : TimelapseMode.Off,
		Math.Clamp(IntervalSeconds, MinInterval, MaxInterval),
		Math.Clamp(FrameRate, MinFrameRate, MaxFrameRate));
}
