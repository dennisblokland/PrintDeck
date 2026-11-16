using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Features.Jobs.Handlers;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Jobs;

public class JobHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private readonly InMemoryGcodeFileStore _files = new();
	private readonly FakeSettingsStore _settings = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Start(string name) => new StartPrintHandler(_session, _files, _settings).Handle(new StartPrintCommand(name), Ct);

	[Fact]
	public async Task Start_streams_the_file_with_its_slicer_estimate()
	{
		_files.Add("cube.gcode", "G28\n", analysis: new GcodeAnalysis(TimeSpan.FromMinutes(42), null, null, 1, null));

		Assert.True((await Start("cube.gcode")).IsSuccess);
		Assert.Equal(("cube.gcode", 4L, (TimeSpan?) TimeSpan.FromMinutes(42)), _session.StartedJob);
	}

	[Fact]
	public async Task Start_passes_the_printer_screen_setting_and_layer_count()
	{
		_files.Add("cube.gcode", "G28\n", analysis: new GcodeAnalysis(null, null, 80, 1, null));

		await Start("cube.gcode");
		Assert.Equal(new PrintOptions(true, 80), _session.StartedWith);

		await _settings.UpdateAsync(s => s with { ShowProgressOnPrinter = false }, Ct);
		await Start("cube.gcode");
		Assert.False(_session.StartedWith!.ShowProgressOnPrinter);
	}

	[Fact]
	public async Task Start_works_without_analysis()
	{
		_files.Add("cube.gcode", "G28\n");
		await Start("cube.gcode");
		Assert.Null(_session.StartedJob!.Value.Estimate);
	}

	[Fact]
	public async Task Start_fails_for_a_missing_file()
	{
		Assert.Equal("File nope.gcode not found.", (await Start("nope.gcode")).Error);
		Assert.Null(_session.StartedJob);
	}

	[Fact]
	public async Task Start_fails_for_an_empty_file()
	{
		_files.Add("empty.gcode", "");
		Assert.Equal("File is empty.", (await Start("empty.gcode")).Error);
		Assert.Null(_session.StartedJob);
	}

	[Fact]
	public async Task Start_passes_on_session_refusal()
	{
		_files.Add("cube.gcode", "G28\n");
		_session.JobResult = Result.Failure("A print is already running.");
		Assert.Equal("A print is already running.", (await Start("cube.gcode")).Error);
	}

	[Fact]
	public async Task Cancel_runs_the_cancel_gcode()
	{
		FakeSettingsStore settings = new(new HostSettings { CancelGcode = "M104 S0\nM140 S0\nG28 X" });

		Assert.True((await new CancelJobHandler(_session, settings).Handle(new CancelJobCommand(), Ct)).IsSuccess);
		Assert.Equal(["M104 S0", "M140 S0", "G28 X"], _session.CancelCommands);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Pause_and_resume_report_the_session_result(bool ok)
	{
		_session.JobResult = ok ? Result.Success() : Result.Failure("Nothing is printing.");

		Assert.Equal(ok, (await new PauseJobHandler(_session).Handle(new PauseJobCommand(), Ct)).IsSuccess);
		Assert.Equal(ok, (await new ResumeJobHandler(_session).Handle(new ResumeJobCommand(), Ct)).IsSuccess);
	}
}
