using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Timelapse.Dtos;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Application.Timelapse;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Settings.Enums;
using PrintDeck.Domain.Timelapse;
using PrintDeck.Domain.Timelapse.Enums;

namespace PrintDeck.Application.Tests.Timelapse;

public sealed class TimelapseRecorderTests : IDisposable
{
	private static readonly DateTimeOffset _t0 = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
	private readonly ManualTimeProvider _time = new(_t0);
	private readonly FakePrinterSession _session = new();
	private readonly PrinterEventHub _events = new();
	private readonly List<PrinterChange> _published = [];
	private readonly FakeSettingsStore _settings = new();
	private readonly FakeCameraSnapshots _camera = new();
	private readonly FakeTimelapseStore _store = new();
	private readonly TimelapseRecorder _recorder;

	public TimelapseRecorderTests()
	{
		_events.Changed += c => { lock (_published) _published.Add(c); };
		_recorder = new TimelapseRecorder(_session, _events, _settings, _camera, _store, _time,
			NullLogger<TimelapseRecorder>.Instance, tick: TimeSpan.FromMilliseconds(5));
		_recorder.Start();
	}

	public void Dispose() => _recorder.Dispose();

	private Task Use(TimelapseMode mode, string? snapshot = "http://cam.local/snap", int interval = 10) =>
		_settings.UpdateAsync(s => s with
		{
			WebcamSnapshotUrl = snapshot,
			WebcamRotation = CameraRotation.Half,
			WebcamFlipHorizontal = true,
			Timelapse = new TimelapseSettings(mode, interval, 30)
		}, TestContext.Current.CancellationToken);

	private async Task<PrintJob> StartPrint(JobKind kind = JobKind.Print)
	{
		PrintJob job = new("parts/cube.gcode", 1000, null, _t0, kind);
		_session.CurrentJob = job;
		_events.Publish(PrinterChange.State | PrinterChange.Job);
		await _recorder.Pending;
		return job;
	}

	private async Task End(Action end)
	{
		end();
		_events.Publish(PrinterChange.State | PrinterChange.Job);
		_events.Publish(PrinterChange.Job); // repeated events end it once
		await _recorder.Pending;
	}

	private async Task Layer(int layer)
	{
		int before = _store.FrameCount;
		_session.Layer = layer;
		_events.Publish(PrinterChange.Layer);
		await Eventually.Until(() => _store.FrameCount > before && _recorder.Status.Frames > before, because: $"no frame for layer {layer}");
	}

	[Fact]
	public void Start_deletes_frames_left_from_before()
	{
		Assert.True(_store.DiscardedAll);
	}

	[Fact]
	public async Task Nothing_is_recorded_while_timelapses_are_off()
	{
		await Use(TimelapseMode.Off);
		PrintJob job = await StartPrint();
		_session.Layer = 1;
		_events.Publish(PrinterChange.Layer);
		await Task.Delay(50, TestContext.Current.CancellationToken);
		await End(() => job.Complete(_t0.AddMinutes(5)));

		Assert.Empty(_camera.Requested);
		Assert.Empty(_store.Rendered);
		Assert.Equal(TimelapseStatus.Idle, _recorder.Status);
	}

	[Fact]
	public async Task Nothing_is_recorded_without_a_snapshot_url()
	{
		await Use(TimelapseMode.Layer, snapshot: null);
		PrintJob job = await StartPrint();
		await End(() => job.Complete(_t0.AddMinutes(5)));

		Assert.Empty(_camera.Requested);
		Assert.Empty(_store.Rendered);
	}

	[Fact]
	public async Task Per_layer_takes_a_frame_at_each_new_layer_and_renders_when_the_print_ends()
	{
		await Use(TimelapseMode.Layer);
		PrintJob job = await StartPrint();
		Assert.Equal("parts/cube.gcode", _recorder.Status.Recording);
		Assert.Contains(PrinterChange.Timelapse, _published);

		await Layer(1);
		await Layer(2);
		await Layer(3);
		Assert.Equal(new TimelapseStatus("parts/cube.gcode", 3, null, null), _recorder.Status);

		await End(() => job.Complete(_t0.AddMinutes(5)));

		// Plus one frame of the finished print.
		(string recording, string name, TimelapseRenderOptions options, int frames) = Assert.Single(_store.Rendered);
		Assert.Equal(4, frames);
		Assert.Equal(TimelapseName.For("cube.gcode", _t0), name);
		Assert.Equal(new TimelapseRenderOptions(30, CameraRotation.Half, true, false), options);
		Assert.Equal([recording], _store.Discarded); // frames go once the video is made
		Assert.Equal(TimelapseStatus.Idle, _recorder.Status);
		Assert.All(_camera.Requested, u => Assert.Equal(new Uri("http://cam.local/snap"), u));
	}

	[Fact]
	public async Task A_cancelled_print_gets_its_video_too_without_a_final_frame()
	{
		await Use(TimelapseMode.Layer);
		PrintJob job = await StartPrint();
		await Layer(1);
		await Layer(2);

		await End(() => job.Cancel(_t0.AddMinutes(1)));

		Assert.Equal(2, Assert.Single(_store.Rendered).Frames);
	}

	[Fact]
	public async Task Layers_while_paused_get_no_frame()
	{
		await Use(TimelapseMode.Layer);
		PrintJob job = await StartPrint();
		await Layer(1);
		job.Pause(_t0.AddMinutes(1));
		_session.Layer = 2;
		_events.Publish(PrinterChange.Layer);
		await Task.Delay(50, TestContext.Current.CancellationToken);

		Assert.Equal(1, _store.FrameCount);
	}

	[Fact]
	public async Task Timed_takes_a_frame_every_interval_of_printing()
	{
		await Use(TimelapseMode.Timed, interval: 10);
		PrintJob job = await StartPrint();
		await Eventually.Until(() => _store.FrameCount == 1, because: "no first frame");

		_time.Advance(TimeSpan.FromSeconds(10));
		await Eventually.Until(() => _store.FrameCount == 2, because: "no second frame");

		job.Pause(_time.Now);
		_events.Publish(PrinterChange.Job);
		await Task.Delay(30, TestContext.Current.CancellationToken);
		_time.Advance(TimeSpan.FromMinutes(5));
		await Task.Delay(50, TestContext.Current.CancellationToken);
		Assert.Equal(2, _store.FrameCount); // paused: the clock stands still

		job.Resume(_time.Now);
		_time.Advance(TimeSpan.FromSeconds(10));
		await Eventually.Until(() => _store.FrameCount == 3, because: "no frame after resuming");

		await End(() => job.Complete(_time.Now));
		Assert.Equal(4, Assert.Single(_store.Rendered).Frames);
	}

	[Fact]
	public async Task Sd_prints_fall_back_to_timed_frames_in_per_layer_mode()
	{
		await Use(TimelapseMode.Layer);
		PrintJob job = await StartPrint(JobKind.SdPrint);

		await Eventually.Until(() => _store.FrameCount == 1, because: "an SD print has no layers to wait for");
		await End(() => job.Complete(_t0.AddMinutes(5)));

		Assert.Equal(2, Assert.Single(_store.Rendered).Frames);
	}

	[Fact]
	public async Task Sd_card_uploads_are_not_recorded()
	{
		await Use(TimelapseMode.Timed);
		await StartPrint(JobKind.SdUpload);
		await Task.Delay(50, TestContext.Current.CancellationToken);

		Assert.Empty(_camera.Requested);
		Assert.Null(_recorder.Status.Recording);
	}

	[Fact]
	public async Task Too_few_frames_make_no_video()
	{
		await Use(TimelapseMode.Layer);
		PrintJob job = await StartPrint();
		await Layer(1);

		await End(() => job.Fail(_t0.AddMinutes(1)));

		Assert.Empty(_store.Rendered);
		Assert.Single(_store.Discarded);
	}

	[Fact]
	public async Task A_camera_failure_is_shown_and_takes_no_frame()
	{
		await Use(TimelapseMode.Layer);
		_camera.Next = Result.Failure<CameraSnapshot>("The camera did not answer.");
		PrintJob job = await StartPrint();
		_session.Layer = 1;
		_events.Publish(PrinterChange.Layer);

		await Eventually.Until(() => _recorder.Status.LastError is not null);
		Assert.Equal("No timelapse frame: The camera did not answer.", _recorder.Status.LastError);
		Assert.Equal(0, _store.FrameCount);

		_camera.Next = Result.Success(new CameraSnapshot([1], "image/jpeg"));
		await Layer(2);
		Assert.Null(_recorder.Status.LastError);
		await End(() => job.Cancel(_t0.AddMinutes(1)));
	}

	[Fact]
	public async Task A_render_failure_is_shown_until_the_next_print()
	{
		await Use(TimelapseMode.Layer);
		_store.RenderResult = Result.Failure<TimelapseVideo>("ffmpeg is not installed, so the timelapse could not be rendered.");
		PrintJob job = await StartPrint();
		await Layer(1);
		await Layer(2);
		await End(() => job.Cancel(_t0.AddMinutes(1)));

		Assert.Equal("ffmpeg is not installed, so the timelapse could not be rendered.", _recorder.Status.LastError);

		await StartPrint();
		Assert.Null(_recorder.Status.LastError);
	}

	[Fact]
	public async Task A_print_that_ends_before_the_recording_starts_is_still_finished()
	{
		await Use(TimelapseMode.Timed);
		PrintJob job = new("cube.gcode", 1000, null, _t0);
		_session.CurrentJob = job;
		_events.Publish(PrinterChange.Job);
		job.Cancel(_t0); // ends before the settings were read
		await _recorder.Pending;

		Assert.Null(_recorder.Status.Recording);
		Assert.Null(_recorder.Status.Rendering);
	}
}
