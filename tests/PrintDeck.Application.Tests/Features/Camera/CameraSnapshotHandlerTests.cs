using PrintDeck.Application.Features.Camera.Commands;
using PrintDeck.Application.Features.Camera.Handlers;
using PrintDeck.Application.Features.Camera.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.Features.Camera;

public class CameraSnapshotHandlerTests
{
	private readonly FakeSettingsStore _settings = new();
	private readonly FakeCameraSnapshots _camera = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result<CameraSnapshot>> Get() => new GetCameraSnapshotHandler(_settings, _camera).Handle(new GetCameraSnapshotQuery(), Ct);
	private ValueTask<Result<CameraSnapshot>> Test(string url) => new TestCameraSnapshotHandler(_camera).Handle(new TestCameraSnapshotCommand(url), Ct);

	[Fact]
	public async Task Fetches_the_saved_snapshot_url()
	{
		await _settings.UpdateAsync(s => s with { WebcamSnapshotUrl = "http://cam.local/?action=snapshot" }, Ct);

		Result<CameraSnapshot> r = await Get();

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal("image/jpeg", r.Value!.ContentType);
		Assert.Equal([new Uri("http://cam.local/?action=snapshot")], _camera.Requested);
	}

	[Fact]
	public async Task Fails_without_a_snapshot_url()
	{
		Assert.Equal("No snapshot URL is set.", (await Get()).Error);
		Assert.Empty(_camera.Requested);
	}

	[Fact]
	public async Task Passes_on_the_camera_failure()
	{
		await _settings.UpdateAsync(s => s with { WebcamSnapshotUrl = "http://cam.local/snap" }, Ct);
		_camera.Next = Result.Failure<CameraSnapshot>("The camera did not answer.");

		Assert.Equal("The camera did not answer.", (await Get()).Error);
	}

	[Fact]
	public async Task Test_fetches_the_given_url_trimmed()
	{
		Assert.True((await Test(" https://cam.local/snap.jpg ")).IsSuccess);
		Assert.Equal([new Uri("https://cam.local/snap.jpg")], _camera.Requested);
	}

	[Theory]
	[InlineData("", "Enter a snapshot URL first.")]
	[InlineData("  ", "Enter a snapshot URL first.")]
	[InlineData("camera/snapshot", "Snapshot URL must be an http or https URL.")]
	[InlineData("file:///etc/passwd", "Snapshot URL must be an http or https URL.")]
	[InlineData("ftp://cam.local/snap.jpg", "Snapshot URL must be an http or https URL.")]
	public async Task Test_refuses_anything_but_a_web_address(string url, string error)
	{
		Assert.Equal(error, (await Test(url)).Error);
		Assert.Empty(_camera.Requested);
	}
}
