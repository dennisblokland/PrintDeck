using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Camera;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.Camera;

public class CameraControlsStartupTests
{
	private readonly FakeSettingsStore _settings = new();
	private readonly FakeCameraControls _camera = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private Task<Result> Apply() =>
		new CameraControlsStartup(_settings, _camera, NullLogger<CameraControlsStartup>.Instance).ApplySavedAsync(Ct);

	[Fact]
	public async Task Applies_the_saved_values_menus_first()
	{
		await _settings.UpdateAsync(s => s with
		{
			CameraDevice = "/dev/video0",
			CameraControls = [new("exposure_time_absolute", 400), new("brightness", 12), new("auto_exposure", 1)]
		}, Ct);

		Assert.True((await Apply()).IsSuccess);
		Assert.Equal(["auto_exposure", "exposure_time_absolute", "brightness"], _camera.Sets.Select(s => s.Value.Name));
		Assert.All(_camera.Sets, s => Assert.Equal("/dev/video0", s.Device));
	}

	[Fact]
	public async Task Does_nothing_without_a_camera_or_saved_values()
	{
		Assert.True((await Apply()).IsSuccess);
		await _settings.UpdateAsync(s => s with { CameraDevice = "/dev/video0" }, Ct);
		Assert.True((await Apply()).IsSuccess);
		Assert.Empty(_camera.Sets);
	}

	[Fact]
	public async Task Reports_a_camera_that_is_not_there()
	{
		await _settings.UpdateAsync(s => s with { CameraDevice = "/dev/video0", CameraControls = [new("brightness", 12)] }, Ct);
		_camera.ListError = "That camera is not connected.";

		Assert.Equal("That camera is not connected.", (await Apply()).Error);
		Assert.Empty(_camera.Sets);
	}
}
