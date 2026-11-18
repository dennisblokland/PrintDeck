using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Camera;

/// <summary>
/// Puts the saved control values back on the camera when PrintDeck starts: most cameras forget them when they
/// lose power. Runs outside a request, so it doesn't go through Mediator; failures are logged, not thrown.
/// </summary>
public sealed class CameraControlsStartup(ISettingsStore settings, ICameraControls camera, ILogger<CameraControlsStartup> logger)
{
	public async Task<Result> ApplySavedAsync(CancellationToken ct)
	{
		HostSettings s = await settings.GetAsync(ct);
		if (s.CameraDevice is not { } device || s.CameraControls.Count == 0) return Result.Success();

		Result<IReadOnlyList<CameraControl>> controls = await camera.ListAsync(device, ct);
		Result result = controls.IsSuccess
			? await camera.SetAsync(device, CameraControl.InApplyOrder(s.CameraControls, controls.Value!), ct)
			: Result.Failure(controls.Error!);

		if (result.IsSuccess) logger.LogInformation("Applied {Count} saved camera controls to {Device}", s.CameraControls.Count, device);
		else logger.LogWarning("Could not apply the saved camera controls to {Device}: {Error}", device, result.Error);
		return result;
	}
}
