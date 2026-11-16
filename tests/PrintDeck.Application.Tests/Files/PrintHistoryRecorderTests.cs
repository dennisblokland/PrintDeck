using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Files;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Application.Tests.Files;

public sealed class PrintHistoryRecorderTests : IDisposable
{
	private static readonly DateTimeOffset _t0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
	private readonly FakePrinterSession _session = new();
	private readonly PrinterEventHub _events = new();
	private readonly List<PrinterChange> _published = [];
	private readonly InMemoryGcodeFileStore _files = new();
	private readonly PrintHistoryRecorder _recorder;

	public PrintHistoryRecorderTests()
	{
		_files.Add("cube.gcode", "G28\n", analysis: new GcodeAnalysis(null, 2000, null, 1, null));
		_files.Add("plain.gcode", "G28\n");
		_events.Changed += c => { lock (_published) _published.Add(c); };
		_recorder = new PrintHistoryRecorder(_session, _events, _files, NullLogger<PrintHistoryRecorder>.Instance);
		_recorder.Start();
	}

	public void Dispose() => _recorder.Dispose();

	private async Task Finish(PrintJob job, Action<PrintJob> end)
	{
		_session.CurrentJob = job;
		_events.Publish(PrinterChange.Job);
		end(job);
		_events.Publish(PrinterChange.Job | PrinterChange.State);
		_events.Publish(PrinterChange.Job); // repeated events record once
		await _recorder.Pending;
	}

	[Fact]
	public async Task Records_a_completed_print_once()
	{
		await Finish(new PrintJob("cube.gcode", 10, null, _t0), j => j.Complete(_t0.AddMinutes(42)));

		PrintRecord record = Assert.Single(_files.PrintsOf("cube.gcode"));
		Assert.Equal(new PrintRecord(_t0, _t0.AddMinutes(42), JobStatus.Completed, TimeSpan.FromMinutes(42), 2000), record);
		Assert.Contains(PrinterChange.Files, _published); // so the file list shows it
	}

	[Fact]
	public async Task Print_time_leaves_out_pauses_and_a_stopped_print_counts_its_share_of_filament()
	{
		await Finish(new PrintJob("cube.gcode", 1000, null, _t0), j =>
		{
			j.Advance(250);
			j.Pause(_t0.AddMinutes(10));
			j.Resume(_t0.AddMinutes(40));
			j.Cancel(_t0.AddMinutes(50));
		});

		PrintRecord record = Assert.Single(_files.PrintsOf("cube.gcode"));
		Assert.Equal(TimeSpan.FromMinutes(20), record.PrintTime);
		Assert.Equal(500, record.FilamentMm); // a quarter of 2000 mm
	}

	[Fact]
	public async Task Filament_stays_unknown_without_an_analysis()
	{
		await Finish(new PrintJob("plain.gcode", 10, null, _t0), j => j.Complete(_t0.AddMinutes(1)));
		Assert.Null(Assert.Single(_files.PrintsOf("plain.gcode")).FilamentMm);
	}

	[Fact]
	public async Task Records_cancelled_and_failed_prints_too()
	{
		await Finish(new PrintJob("cube.gcode", 10, null, _t0), j => j.Cancel(_t0.AddMinutes(1)));
		await Finish(new PrintJob("cube.gcode", 10, null, _t0.AddHours(1)), j => j.Fail(_t0.AddHours(2)));

		Assert.Equal([JobStatus.Cancelled, JobStatus.Failed], _files.PrintsOf("cube.gcode").Select(p => p.Status));
	}

	[Fact]
	public async Task Ignores_running_jobs_sd_prints_and_uploads()
	{
		_session.CurrentJob = new PrintJob("cube.gcode", 10, null, _t0);
		_events.Publish(PrinterChange.Job);
		await Finish(new PrintJob("cube.gcode", 10, null, _t0, JobKind.SdPrint), j => j.Complete(_t0));
		await Finish(new PrintJob("cube.gcode", 10, null, _t0, JobKind.SdUpload), j => j.Complete(_t0));

		Assert.Empty(_files.PrintsOf("cube.gcode"));
	}

	[Fact]
	public async Task Other_changes_are_ignored()
	{
		PrintJob job = new("cube.gcode", 10, null, _t0);
		job.Complete(_t0);
		_session.CurrentJob = job;
		_events.Publish(PrinterChange.Temperature | PrinterChange.Terminal);
		await _recorder.Pending;

		Assert.Empty(_files.PrintsOf("cube.gcode"));
	}
}
