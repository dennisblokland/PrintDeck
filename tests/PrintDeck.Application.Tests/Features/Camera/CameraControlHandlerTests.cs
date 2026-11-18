using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Camera.Commands;
using PrintDeck.Application.Features.Camera.Dtos;
using PrintDeck.Application.Features.Camera.Handlers;
using PrintDeck.Application.Features.Camera.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.Features.Camera;

public class CameraControlHandlerTests
{
	private readonly FakeSettingsStore _settings = new();
	private readonly FakeCameraControls _camera = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<CameraControlsDto> Get() => new GetCameraControlsHandler(_settings, _camera).Handle(new GetCameraControlsQuery(), Ct);
	private ValueTask<Result> Select(string? device) => new SelectCameraDeviceHandler(_settings, _camera, _events.Hub).Handle(new SelectCameraDeviceCommand(device), Ct);
	private ValueTask<Result> Set(string name, long value) => new SetCameraControlHandler(_settings, _camera, _events.Hub).Handle(new SetCameraControlCommand(name, value), Ct);
	private ValueTask<Result> Reset() => new ResetCameraControlsHandler(_settings, _camera, _events.Hub).Handle(new ResetCameraControlsCommand(), Ct);

	private Task UseCamera(string device = "/dev/video0") => _settings.UpdateAsync(s => s with { CameraDevice = device }, Ct);

	[Fact]
	public async Task Without_a_camera_lists_the_devices_only()
	{
		CameraControlsDto dto = await Get();

		Assert.Null(dto.Device);
		Assert.Equal(["/dev/video0", "VIRTUAL"], dto.Devices);
		Assert.Empty(dto.Controls);
		Assert.Null(dto.Error);
	}

	[Fact]
	public async Task Lists_the_controls_of_the_chosen_camera()
	{
		await UseCamera();

		CameraControlsDto dto = await Get();

		Assert.Equal("/dev/video0", dto.Device);
		Assert.Equal(["brightness", "auto_exposure", "exposure_time_absolute"], dto.Controls.Select(c => c.Name));
	}

	[Fact]
	public async Task Shows_why_the_controls_could_not_be_read()
	{
		await UseCamera();
		_camera.ListError = "The camera did not answer.";

		CameraControlsDto dto = await Get();

		Assert.Empty(dto.Controls);
		Assert.Equal("The camera did not answer.", dto.Error);
	}

	[Fact]
	public async Task Selecting_another_camera_forgets_the_saved_values()
	{
		await _settings.UpdateAsync(s => s with { CameraDevice = "/dev/video0", CameraControls = [new("brightness", 5)] }, Ct);

		Assert.True((await Select("/dev/video0")).IsSuccess);
		Assert.Single(_settings.Settings.CameraControls);

		Assert.True((await Select(" VIRTUAL ")).IsSuccess);
		Assert.Equal("VIRTUAL", _settings.Settings.CameraDevice);
		Assert.Empty(_settings.Settings.CameraControls);
		Assert.Contains(PrinterChange.Settings, _events.Published);

		Assert.True((await Select("")).IsSuccess);
		Assert.Null(_settings.Settings.CameraDevice);
	}

	[Fact]
	public async Task Refuses_a_camera_that_is_not_connected()
	{
		Assert.Equal("That camera is not connected.", (await Select("/dev/video7")).Error);
		Assert.Equal(0, _settings.Saves);
	}

	[Fact]
	public async Task Sets_a_control_and_remembers_it()
	{
		await UseCamera();
		await _settings.UpdateAsync(s => s with { CameraControls = [new("brightness", 5), new("auto_exposure", 1)] }, Ct);

		Result r = await Set("brightness", 20);

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal([("/dev/video0", new CameraControlValue("brightness", 20))], _camera.Sets);
		Assert.Equal([new CameraControlValue("auto_exposure", 1), new CameraControlValue("brightness", 20)], _settings.Settings.CameraControls);
		Assert.Contains(PrinterChange.Settings, _events.Published);
	}

	public static TheoryData<string, string> Refused => new()
	{
		{ "no camera", "Choose a camera first." },
		{ "unknown control", "This camera has no control 'zoom_absolute'." },
		{ "out of range", "Brightness must be between -64 and 64." },
		{ "not a menu option", "Auto exposure has no option 2." },
		{ "camera unreachable", "The camera did not answer." },
		{ "camera refuses", "The camera refused: Numerical result out of range" },
	};

	[Theory]
	[MemberData(nameof(Refused))]
	public async Task Refuses_what_the_camera_would_not_take(string reason, string error)
	{
		if (reason != "no camera") await UseCamera();
		int saves = _settings.Saves;
		Result r = reason switch
		{
			"no camera" => await Set("brightness", 1),
			"unknown control" => await Set("zoom_absolute", 100),
			"out of range" => await Set("brightness", 65),
			"not a menu option" => await Set("auto_exposure", 2),
			"camera unreachable" => await WithListError(),
			_ => await WithSetError()
		};

		Assert.Equal(error, r.Error);
		Assert.Equal(saves, _settings.Saves);
		Assert.Empty(_events.Published);

		async Task<Result> WithListError()
		{
			_camera.ListError = "The camera did not answer.";
			return await Set("brightness", 1);
		}

		async Task<Result> WithSetError()
		{
			_camera.SetError = "The camera refused: Numerical result out of range";
			return await Set("brightness", 1);
		}
	}

	[Fact]
	public async Task Reset_sets_every_default_with_menus_first_and_forgets_saved_values()
	{
		await _settings.UpdateAsync(s => s with { CameraDevice = "/dev/video0", CameraControls = [new("brightness", 5)] }, Ct);

		Assert.True((await Reset()).IsSuccess);

		Assert.Equal(["auto_exposure=3", "brightness=0", "exposure_time_absolute=250"], _camera.Sets.Select(s => $"{s.Value.Name}={s.Value.Value}"));
		Assert.Empty(_settings.Settings.CameraControls);
	}

	[Fact]
	public async Task Reset_needs_a_camera()
	{
		Assert.Equal("Choose a camera first.", (await Reset()).Error);
	}
}
