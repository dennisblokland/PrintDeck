using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Camera.Queries;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Camera.Handlers;

public sealed class GetCameraSnapshotHandler(ISettingsStore settings, ICameraSnapshots camera)
	: IQueryHandler<GetCameraSnapshotQuery, Result<CameraSnapshot>>
{
	public const string NotSet = "No snapshot URL is set.";

	public async ValueTask<Result<CameraSnapshot>> Handle(GetCameraSnapshotQuery q, CancellationToken ct)
	{
		HostSettings s = await settings.GetAsync(ct);
		if (string.IsNullOrWhiteSpace(s.WebcamSnapshotUrl)) return Result.Failure<CameraSnapshot>(NotSet);
		if (SnapshotSource.Parse(s.WebcamSnapshotUrl) is not { } url) return Result.Failure<CameraSnapshot>(SnapshotSource.Invalid);
		return await camera.TakeAsync(url, ct);
	}
}
