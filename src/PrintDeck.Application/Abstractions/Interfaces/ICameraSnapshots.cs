using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Fetches a still image from a camera's snapshot URL.</summary>
public interface ICameraSnapshots
{
	/// <summary>The image at <paramref name="url"/>; a failure with a short reason when the camera is unreachable or sends no image.</summary>
	Task<Result<CameraSnapshot>> TakeAsync(Uri url, CancellationToken ct);
}
