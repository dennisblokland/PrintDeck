using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Camera.Dtos;
using PrintDeck.Application.Features.Camera.Queries;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Camera.Handlers;

public sealed class GetCameraControlsHandler(ISettingsStore settings, ICameraControls camera)
	: IQueryHandler<GetCameraControlsQuery, CameraControlsDto>
{
	public async ValueTask<CameraControlsDto> Handle(GetCameraControlsQuery q, CancellationToken ct)
	{
		IReadOnlyList<string> devices = camera.Devices();
		string? device = (await settings.GetAsync(ct)).CameraDevice;
		if (device is null) return new CameraControlsDto(null, devices, [], null);

		Result<IReadOnlyList<CameraControl>> controls = await camera.ListAsync(device, ct);
		return controls.IsSuccess
			? new CameraControlsDto(device, devices, controls.Value!, null)
			: new CameraControlsDto(device, devices, [], controls.Error);
	}
}
