using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Plugins;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Application.Timelapse;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Timelapse;
using PrintDeck.Domain.Timelapse.Enums;
using PrintDeck.Plugins;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Application.Tests.Plugins;

public sealed class PrinterEventStreamTests : IDisposable
{
	private static readonly DateTimeOffset _t0 = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
	private readonly ManualTimeProvider _time = new(_t0);
	private readonly FakePrinterSession _session = new() { State = PrinterState.Offline };
	private readonly PrinterEventHub _hub = new();
	private readonly FakeSettingsStore _settings = new();
	private readonly TimelapseRecorder _timelapse;
	private readonly PrinterEventStream _stream;
	private readonly List<PrinterEvent> _seen = [];

	public PrinterEventStreamTests()
	{
		_timelapse = new TimelapseRecorder(_session, _hub, _settings, new FakeCameraSnapshots(), new FakeTimelapseStore(), _time,
			NullLogger<TimelapseRecorder>.Instance, tick: TimeSpan.FromMilliseconds(5));
		_stream = new PrinterEventStream(_session, _hub, _timelapse, _time);
		_stream.Start();
		_stream.Subscribe(e => { lock (_seen) _seen.Add(e); });
	}

	public void Dispose()
	{
		_stream.Dispose();
		_timelapse.Dispose();
	}

	private IReadOnlyList<PrinterEventKind> Kinds()
	{
		lock (_seen) return _seen.Select(e => e.Kind).ToList();
	}

	private void Set(PrinterState state, PrinterChange change = PrinterChange.State)
	{
		_session.State = state;
		_hub.Publish(change);
	}

	private PrintJob StartJob(JobKind kind = JobKind.Print, string file = "cube.gcode")
	{
		PrintJob job = new(file, 1000, null, _time.GetUtcNow(), kind);
		_session.CurrentJob = job;
		Set(kind == JobKind.SdUpload ? PrinterState.Transferring : PrinterState.Printing, PrinterChange.State | PrinterChange.Job);
		return job;
	}

	[Fact]
	public void Connecting_and_disconnecting()
	{
		Set(PrinterState.Connecting);
		Set(PrinterState.Operational);
		Set(PrinterState.Operational);
		Set(PrinterState.Error);
		Set(PrinterState.Offline);

		Assert.Equal([PrinterEventKind.Connected, PrinterEventKind.Error, PrinterEventKind.Disconnected], Kinds());
	}

	[Fact]
	public void A_print_from_start_to_done_each_event_once()
	{
		Set(PrinterState.Operational);
		PrintJob job = StartJob();
		job.Pause(_time.GetUtcNow());
		Set(PrinterState.Paused, PrinterChange.State | PrinterChange.Job);
		job.Resume(_time.GetUtcNow());
		Set(PrinterState.Printing, PrinterChange.Job);
		_hub.Publish(PrinterChange.Job | PrinterChange.Temperature);
		job.Complete(_time.GetUtcNow());
		Set(PrinterState.Operational, PrinterChange.State | PrinterChange.Job);
		_hub.Publish(PrinterChange.Job);

		Assert.Equal(
		[
			PrinterEventKind.Connected, PrinterEventKind.PrintStarted, PrinterEventKind.PrintPaused,
			PrinterEventKind.PrintResumed, PrinterEventKind.PrintDone
		], Kinds());
		lock (_seen) Assert.All(_seen.Skip(1), e => Assert.Equal("cube.gcode", e.FileName));
	}

	[Fact]
	public void Cancelled_and_failed_prints()
	{
		Set(PrinterState.Operational);
		StartJob().Cancel(_time.GetUtcNow());
		_hub.Publish(PrinterChange.Job);
		StartJob(JobKind.SdPrint, "SD.GCO").Fail(_time.GetUtcNow());
		_hub.Publish(PrinterChange.Job);

		Assert.Equal(
		[
			PrinterEventKind.Connected, PrinterEventKind.PrintStarted, PrinterEventKind.PrintCancelled,
			PrinterEventKind.PrintStarted, PrinterEventKind.PrintFailed
		], Kinds());
	}

	[Fact]
	public void Copying_to_the_sd_card_is_not_a_print()
	{
		Set(PrinterState.Operational);
		StartJob(JobKind.SdUpload).Complete(_time.GetUtcNow());
		Set(PrinterState.Operational, PrinterChange.State | PrinterChange.Job);

		Assert.Equal([PrinterEventKind.Connected], Kinds());
	}

	[Fact]
	public void A_print_replaced_before_its_end_was_seen_still_ends()
	{
		Set(PrinterState.Operational);
		StartJob(file: "a.gcode").Complete(_time.GetUtcNow());
		StartJob(file: "b.gcode");

		lock (_seen)
			Assert.Equal(["Connected ", "PrintStarted a.gcode", "PrintDone a.gcode", "PrintStarted b.gcode"],
				_seen.Select(e => $"{e.Kind} {e.FileName}"));
	}

	[Fact]
	public void Settings_changes_pass_through()
	{
		_hub.Publish(PrinterChange.Settings);

		Assert.Equal([PrinterEventKind.SettingsChanged], Kinds());
	}

	[Fact]
	public async Task A_timelapse_starts_and_stops_once_rendered()
	{
		await _settings.UpdateAsync(s => s with
		{
			WebcamSnapshotUrl = "http://cam.local/snap",
			Timelapse = new TimelapseSettings(TimelapseMode.Timed, 10, 30)
		}, TestContext.Current.CancellationToken);
		_timelapse.Start();
		Set(PrinterState.Operational);

		PrintJob job = StartJob();
		await _timelapse.Pending;
		Assert.Contains(PrinterEventKind.TimelapseStarted, Kinds());

		job.Complete(_time.GetUtcNow());
		Set(PrinterState.Operational, PrinterChange.State | PrinterChange.Job);
		await _timelapse.Pending;

		Assert.Equal(
		[
			PrinterEventKind.Connected, PrinterEventKind.PrintStarted, PrinterEventKind.TimelapseStarted,
			PrinterEventKind.PrintDone, PrinterEventKind.TimelapseStopped
		], Kinds());
	}

	[Fact]
	public void A_broken_subscriber_does_not_stop_the_others()
	{
		List<PrinterEventKind> other = [];
		using IDisposable broken = _stream.Subscribe(_ => throw new InvalidOperationException("broken"));
		using IDisposable fine = _stream.Subscribe(e => other.Add(e.Kind));

		_hub.Publish(PrinterChange.Settings);

		Assert.Equal([PrinterEventKind.SettingsChanged], other);
	}

	[Fact]
	public void Disposing_a_subscription_ends_it()
	{
		List<PrinterEventKind> got = [];
		IDisposable subscription = _stream.Subscribe(e => got.Add(e.Kind));

		subscription.Dispose();
		_hub.Publish(PrinterChange.Settings);

		Assert.Empty(got);
	}

	[Fact]
	public void Starts_from_what_the_printer_is_already_doing()
	{
		PrintJob running = new("old.gcode", 1000, null, _t0);
		FakePrinterSession session = new() { State = PrinterState.Printing, CurrentJob = running };
		PrinterEventHub hub = new();
		using PrinterEventStream stream = new(session, hub, _timelapse, _time);
		List<PrinterEventKind> got = [];
		stream.Start();
		stream.Subscribe(e => got.Add(e.Kind));

		hub.Publish(PrinterChange.State | PrinterChange.Job);
		running.Complete(_t0);
		hub.Publish(PrinterChange.Job);

		Assert.Equal([PrinterEventKind.PrintDone], got);
	}
}
