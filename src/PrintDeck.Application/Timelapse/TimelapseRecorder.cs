using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Timelapse.Dtos;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Timelapse;
using PrintDeck.Domain.Timelapse.Enums;

namespace PrintDeck.Application.Timelapse;

/// <summary>
/// Records a timelapse of each print (streamed or from the SD card) while it is switched on: frames from the snapshot
/// URL, every few seconds of printing or at each new layer, rendered to a video when the print ends, however it ends.
/// Runs outside a request, so it doesn't go through Mediator; failures are logged and shown, never thrown.
/// </summary>
public sealed class TimelapseRecorder(
	IPrinterSession session,
	PrinterEventHub events,
	ISettingsStore settings,
	ICameraSnapshots camera,
	ITimelapseStore store,
	TimeProvider time,
	ILogger<TimelapseRecorder> logger,
	TimeSpan? tick = null) : IDisposable
{
	/// <summary>A video needs at least this many frames; fewer are thrown away.</summary>
	public const int MinFrames = 2;

	private readonly TimeSpan _tick = tick ?? TimeSpan.FromSeconds(1);
	private readonly Lock _gate = new();
	private PrintJob? _seen;
	private TimelapseRecording? _current;
	private Task _work = Task.CompletedTask;
	private string? _rendering;
	private int _renderingFrames;
	private string? _lastError;

	public void Start()
	{
		try { store.DiscardAllFrames(); }
		catch (Exception ex) { logger.LogWarning(ex, "Could not delete the frames of unfinished timelapses"); }
		events.Changed += OnChanged;
	}

	/// <summary>Completes once queued starts, ends and renders are done (for tests).</summary>
	public Task Pending
	{
		get { lock (_gate) return _work; }
	}

	public TimelapseStatus Status
	{
		get
		{
			lock (_gate)
			{
				if (_current is { } r) return new TimelapseStatus(r.Job.FileName, Volatile.Read(ref r.Frames), _rendering, _lastError);
				return new TimelapseStatus(null, _renderingFrames, _rendering, _lastError);
			}
		}
	}

	private void OnChanged(PrinterChange change)
	{
		if ((change & (PrinterChange.Job | PrinterChange.State)) != 0) FollowJob();
		if ((change & PrinterChange.Layer) != 0) OnLayer();
	}

	private void FollowJob()
	{
		PrintJob? job = session.CurrentJob;
		TimelapseRecording? ended = null;
		lock (_gate)
		{
			if (_current is { } r && !r.Job.IsActive) ended = Detach(r);
			if (job is { IsActive: true, Kind: JobKind.Print or JobKind.SdPrint } && !ReferenceEquals(job, _seen))
			{
				_seen = job;
				Queue(() => BeginAsync(job));
			}
		}
		if (ended is not null) Queue(() => FinishAsync(ended));
	}

	private void OnLayer()
	{
		TimelapseRecording? r;
		lock (_gate) r = _current;
		if (r is not null && r.Job.Status == JobStatus.Printing && r.Schedule.OnLayer(session.Layer)) Capture(r);
	}

	private void Queue(Func<Task> work)
	{
		lock (_gate) _work = _work.ContinueWith(_ => work(), TaskScheduler.Default).Unwrap();
	}

	/// <summary>Takes <paramref name="r"/> off the recorder; null when it already was. Call under the lock.</summary>
	private TimelapseRecording? Detach(TimelapseRecording r)
	{
		if (!ReferenceEquals(_current, r)) return null;
		_current = null;
		_rendering = r.Job.FileName;
		_renderingFrames = Volatile.Read(ref r.Frames);
		r.Stop.Cancel();
		return r;
	}

	private async Task BeginAsync(PrintJob job)
	{
		try
		{
			HostSettings s = await settings.GetAsync(CancellationToken.None);
			TimelapseSettings t = s.Timelapse;
			if (t.Mode == TimelapseMode.Off || SnapshotSource.Parse(s.WebcamSnapshotUrl) is not { } url) return;

			// The printer reads SD card files itself, so PrintDeck can't see their layers.
			TimelapseMode mode = t.Mode == TimelapseMode.Layer && job.Kind == JobKind.SdPrint ? TimelapseMode.Timed : t.Mode;
			TimelapseRecording r = new(job, url,
				new TimelapseSchedule(mode, TimeSpan.FromSeconds(t.IntervalSeconds)),
				new TimelapseRenderOptions(t.FrameRate, s.WebcamRotation, s.WebcamFlipHorizontal, s.WebcamFlipVertical));

			TimelapseRecording? ended = null;
			lock (_gate)
			{
				if (_current is { } previous) ended = Detach(previous);
				_current = r;
				_lastError = null;
				if (mode == TimelapseMode.Timed) r.Loop = Task.Run(() => TickAsync(r));
				// The print may have ended while the settings were read: no event would end the recording then.
				if (!job.IsActive) ended = Detach(r);
			}
			logger.LogInformation("Recording a timelapse of {File}, {Mode}", job.FileName, mode == TimelapseMode.Timed ? $"every {t.IntervalSeconds} s" : "per layer");
			events.Publish(PrinterChange.Timelapse);
			if (ended is not null) await FinishAsync(ended);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not start the timelapse of {File}", job.FileName);
		}
	}

	private async Task TickAsync(TimelapseRecording r)
	{
		try
		{
			while (!r.Stop.IsCancellationRequested)
			{
				if (r.Schedule.Tick(time.GetUtcNow(), r.Job.Status == JobStatus.Printing)) Capture(r);
				await Task.Delay(_tick, r.Stop.Token);
			}
		}
		catch (OperationCanceledException) { }
	}

	private void Capture(TimelapseRecording r)
	{
		if (Interlocked.CompareExchange(ref r.Capturing, 1, 0) != 0) return; // still taking the last one
		// Off the caller's thread: layer changes arrive on the printer's send loop.
		Task capture = Task.Run(() => CaptureAsync(r));
		lock (_gate) r.LastCapture = capture;
	}

	private async Task CaptureAsync(TimelapseRecording r)
	{
		try
		{
			Result<CameraSnapshot> shot = await camera.TakeAsync(r.Camera, CancellationToken.None);
			if (!shot.IsSuccess)
			{
				Fail($"No timelapse frame: {shot.Error}");
				return;
			}
			int number = Volatile.Read(ref r.Frames) + 1;
			await store.SaveFrameAsync(r.Id, number, shot.Value!, CancellationToken.None);
			Volatile.Write(ref r.Frames, number);
			lock (_gate) if (ReferenceEquals(_current, r)) _lastError = null;
			events.Publish(PrinterChange.Timelapse);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not save a timelapse frame");
			Fail("Could not save a timelapse frame.");
		}
		finally
		{
			Volatile.Write(ref r.Capturing, 0);
		}
	}

	private void Fail(string error)
	{
		lock (_gate)
		{
			if (_lastError == error) return;
			_lastError = error;
		}
		logger.LogWarning("{Error}", error);
		events.Publish(PrinterChange.Timelapse);
	}

	private async Task FinishAsync(TimelapseRecording r)
	{
		events.Publish(PrinterChange.Timelapse);
		try
		{
			await r.Loop;
			Task last;
			lock (_gate) last = r.LastCapture;
			await last;

			// The finished print is the frame that matters most.
			if (r.Job.Status == JobStatus.Completed && Interlocked.CompareExchange(ref r.Capturing, 1, 0) == 0) await CaptureAsync(r);

			int frames = Volatile.Read(ref r.Frames);
			if (frames < MinFrames)
			{
				logger.LogInformation("No timelapse of {File}: only {Frames} frame(s)", r.Job.FileName, frames);
				return;
			}

			string name = TimelapseName.For(r.Job.FileName, r.Job.StartedAt);
			Result<TimelapseVideo> video = await store.RenderAsync(r.Id, name, r.Render, CancellationToken.None);
			if (video.IsSuccess) logger.LogInformation("Rendered the timelapse {Video} from {Frames} frames", video.Value!.Name, frames);
			else
			{
				logger.LogWarning("Could not render the timelapse of {File}: {Error}", r.Job.FileName, video.Error);
				lock (_gate) _lastError = video.Error;
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not render the timelapse of {File}", r.Job.FileName);
			lock (_gate) _lastError = "Could not render the timelapse.";
		}
		finally
		{
			try { store.DiscardFrames(r.Id); }
			catch (Exception ex) { logger.LogWarning(ex, "Could not delete the timelapse frames"); }
			r.Stop.Dispose();
			lock (_gate)
			{
				_rendering = null;
				_renderingFrames = 0;
			}
			events.Publish(PrinterChange.Timelapse);
		}
	}

	public void Dispose()
	{
		events.Changed -= OnChanged;
		lock (_gate) _current?.Stop.Cancel();
	}
}
