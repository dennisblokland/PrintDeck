using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Infrastructure.Camera;

/// <summary>Sends <see cref="SnapshotSource.VirtualUri"/> to the virtual camera and every other URL to the real one.</summary>
public sealed class CameraSnapshots(ICameraSnapshots http, ICameraSnapshots virtualCamera) : ICameraSnapshots
{
	public Task<Result<CameraSnapshot>> TakeAsync(Uri url, CancellationToken ct) =>
		url == SnapshotSource.VirtualUri ? virtualCamera.TakeAsync(url, ct) : http.TakeAsync(url, ct);
}
