using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Camera.Commands;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Handlers;

public sealed class SelectCameraDeviceHandler(ISettingsStore settings, ICameraControls camera, PrinterEventHub events)
	: ICommandHandler<SelectCameraDeviceCommand, Result>
{
	public async ValueTask<Result> Handle(SelectCameraDeviceCommand c, CancellationToken ct)
	{
		string? device = string.IsNullOrWhiteSpace(c.Device) ? null : c.Device.Trim();
		if (device is not null && !camera.Devices().Contains(device)) return Result.Failure("That camera is not connected.");

		await settings.UpdateAsync(s => s.CameraDevice == device ? s : s with { CameraDevice = device, CameraControls = [] }, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
