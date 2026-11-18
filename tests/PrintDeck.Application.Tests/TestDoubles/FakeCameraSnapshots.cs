using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeCameraSnapshots : ICameraSnapshots
{
	public Result<CameraSnapshot> Next { get; set; } = Result.Success(new CameraSnapshot([0xFF, 0xD8, 0xFF], "image/jpeg"));
	public List<Uri> Requested { get; } = [];

	public Task<Result<CameraSnapshot>> TakeAsync(Uri url, CancellationToken ct)
	{
		Requested.Add(url);
		return Task.FromResult(Next);
	}
}
