using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Camera.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Handlers;

public sealed class TestCameraSnapshotHandler(ICameraSnapshots camera)
	: ICommandHandler<TestCameraSnapshotCommand, Result<CameraSnapshot>>
{
	public async ValueTask<Result<CameraSnapshot>> Handle(TestCameraSnapshotCommand c, CancellationToken ct)
	{
		string url = c.Url?.Trim() ?? "";
		if (url.Length == 0) return Result.Failure<CameraSnapshot>("Enter a snapshot URL first.");
		if (!UpdateSettingsHandler.IsHttpUrl(url)) return Result.Failure<CameraSnapshot>("Snapshot URL must be an http or https URL.");
		return await camera.TakeAsync(new Uri(url), ct);
	}
}
