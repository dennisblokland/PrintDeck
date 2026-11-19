using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Infrastructure.Camera;

namespace PrintDeck.Infrastructure.Tests.Camera;

public class CameraSnapshotsTests
{
	private sealed class Named(string type) : ICameraSnapshots
	{
		public Task<Result<CameraSnapshot>> TakeAsync(Uri url, CancellationToken ct) => Task.FromResult(Result.Success(new CameraSnapshot([1], type)));
	}

	private readonly CameraSnapshots _cameras = new(new Named("image/http"), new Named("image/virtual"));

	[Fact]
	public async Task Virtual_goes_to_the_pretend_camera_everything_else_over_http()
	{
		Assert.Equal("image/virtual", (await _cameras.TakeAsync(SnapshotSource.VirtualUri, TestContext.Current.CancellationToken)).Value!.ContentType);
		Assert.Equal("image/http", (await _cameras.TakeAsync(new Uri("http://cam.local/snap"), TestContext.Current.CancellationToken)).Value!.ContentType);
	}
}
