using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Frames of the timelapse being recorded, and the rendered videos.</summary>
public interface ITimelapseStore
{
	/// <summary>Keeps frame <paramref name="number"/> (counting from 1, no gaps) of <paramref name="recording"/>.</summary>
	Task SaveFrameAsync(string recording, int number, CameraSnapshot frame, CancellationToken ct);

	/// <summary>Turns the frames of <paramref name="recording"/> into a video called <paramref name="videoName"/> (or a free variant of it).</summary>
	Task<Result<TimelapseVideo>> RenderAsync(string recording, string videoName, TimelapseRenderOptions options, CancellationToken ct);

	/// <summary>Deletes the frames of <paramref name="recording"/>, rendered or not.</summary>
	void DiscardFrames(string recording);

	/// <summary>Deletes frames left over from recordings that never finished (PrintDeck stopped mid-print).</summary>
	void DiscardAllFrames();

	/// <summary>Rendered videos, newest first.</summary>
	IReadOnlyList<TimelapseVideo> List();

	/// <summary>The video's content, or null when there is no such video.</summary>
	Stream? OpenRead(string name);

	/// <summary>False when there is no such video.</summary>
	bool Delete(string name);
}
