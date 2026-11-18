using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Camera.Commands;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Handlers;

public sealed class SetCameraControlHandler(ISettingsStore settings, ICameraControls camera, PrinterEventHub events)
	: ICommandHandler<SetCameraControlCommand, Result>
{
	public async ValueTask<Result> Handle(SetCameraControlCommand c, CancellationToken ct)
	{
		if ((await settings.GetAsync(ct)).CameraDevice is not { } device) return Result.Failure("Choose a camera first.");

		Result<IReadOnlyList<CameraControl>> controls = await camera.ListAsync(device, ct);
		if (!controls.IsSuccess) return Result.Failure(controls.Error!);
		if (controls.Value!.FirstOrDefault(x => x.Name == c.Name) is not { } control)
			return Result.Failure($"This camera has no control '{c.Name}'.");
		if (control.Check(c.Value) is { } error) return Result.Failure(error);

		Result set = await camera.SetAsync(device, [new CameraControlValue(control.Name, c.Value)], ct);
		if (!set.IsSuccess) return set;

		await settings.UpdateAsync(s => s with
		{
			CameraControls = [.. s.CameraControls.Where(v => v.Name != control.Name), new CameraControlValue(control.Name, c.Value)]
		}, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
