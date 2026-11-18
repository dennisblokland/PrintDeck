using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>The v4l2 controls of the host's cameras (and of the virtual camera).</summary>
public interface ICameraControls
{
	/// <summary>Camera devices PrintDeck may adjust; only these are passed to the camera tools.</summary>
	IReadOnlyList<string> Devices();

	Task<Result<IReadOnlyList<CameraControl>>> ListAsync(string device, CancellationToken ct);

	/// <summary>Sets the values one by one, in the order given; stops at the first the camera refuses.</summary>
	Task<Result> SetAsync(string device, IReadOnlyList<CameraControlValue> values, CancellationToken ct);
}
