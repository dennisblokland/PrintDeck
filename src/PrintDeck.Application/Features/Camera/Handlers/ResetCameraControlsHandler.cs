using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Camera.Commands;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Handlers;

public sealed class ResetCameraControlsHandler(ISettingsStore settings, ICameraControls camera, PrinterEventHub events)
	: ICommandHandler<ResetCameraControlsCommand, Result>
{
	public async ValueTask<Result> Handle(ResetCameraControlsCommand c, CancellationToken ct)
	{
		if ((await settings.GetAsync(ct)).CameraDevice is not { } device) return Result.Failure("Choose a camera first.");

		Result<IReadOnlyList<CameraControl>> controls = await camera.ListAsync(device, ct);
		if (!controls.IsSuccess) return Result.Failure(controls.Error!);

		IReadOnlyList<CameraControlValue> defaults = CameraControl.InApplyOrder(
			controls.Value!.Select(x => new CameraControlValue(x.Name, x.Default)), controls.Value!);
		Result set = await camera.SetAsync(device, defaults, ct);
		if (!set.IsSuccess) return set;

		await settings.UpdateAsync(s => s with { CameraControls = [] }, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
