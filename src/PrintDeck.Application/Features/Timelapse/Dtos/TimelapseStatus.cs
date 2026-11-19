namespace PrintDeck.Application.Features.Timelapse.Dtos;

/// <summary>What the timelapse recorder is doing.</summary>
/// <param name="Recording">The printed file while frames are being taken, otherwise null.</param>
/// <param name="Frames">Frames taken so far of the recording (or of the one being rendered).</param>
/// <param name="Rendering">The printed file whose timelapse is being rendered, otherwise null.</param>
/// <param name="LastError">Why the last frame or video failed, until the next one succeeds.</param>
public sealed record TimelapseStatus(string? Recording, int Frames, string? Rendering, string? LastError)
{
	public static readonly TimelapseStatus Idle = new(null, 0, null, null);
}
