using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Features.Jobs.Handlers;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Jobs;

public class JobHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private readonly InMemoryGcodeFileStore _files = new();
	private readonly FakeSettingsStore _settings = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Start(string name, bool ignoreProblems = false) =>
		new StartPrintHandler(_session, _files, _settings).Handle(new StartPrintCommand(name, ignoreProblems), Ct);

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
		Assert.Equal((true, 80), (_session.StartedWith!.ShowProgressOnPrinter, _session.StartedWith.LayerCount));

		await _settings.UpdateAsync(s => s with { ShowProgressOnPrinter = false }, Ct);
		await Start("cube.gcode");
		Assert.False(_session.StartedWith!.ShowProgressOnPrinter);
	}

	[Fact]
	public async Task Start_refuses_a_file_with_an_error()
	{
		_files.Add("bad.gcode", "G28\nM104 S{material_print_temperature}\n");

		Assert.Equal("Not printing bad.gcode: The slicer left {material_print_temperature} unfilled; the printer reads it as 0.",
			(await Start("bad.gcode")).Error);
		Assert.Null(_session.StartedJob);
	}

	[Fact]
	public async Task Start_prints_a_file_with_only_warnings()
	{
		_files.Add("warm.gcode", "G28\nG1 X10 Y10 E1\n"); // extrudes without a temperature: a warning
		Assert.True((await Start("warm.gcode")).IsSuccess);
	}

	[Fact]
	public async Task Start_prints_anyway_when_told_to()
	{
		_files.Add("bad.gcode", "G28\nM112\n");
		Assert.True((await Start("bad.gcode", ignoreProblems: true)).IsSuccess);
		Assert.NotNull(_session.StartedJob);
	}

	[Fact]
	public async Task Start_leaves_the_refusal_to_a_busy_printer()
	{
		_files.Add("bad.gcode", "G28\nM112\n");
		_session.State = PrinterState.Printing;
		_session.JobResult = Result.Failure("A print is already running.");
		Assert.Equal("A print is already running.", (await Start("bad.gcode")).Error);
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

	[Fact]
	public async Task Cancel_fills_in_snippets()
	{
		FakeSettingsStore settings = new(new HostSettings { CancelGcode = "{% snippet 'disable_bed' %}\nM84" });

		await new CancelJobHandler(_session, settings).Handle(new CancelJobCommand(), Ct);

		Assert.Equal(["M140 S0", "M84"], _session.CancelCommands);
	}

	[Fact]
	public async Task Start_passes_the_scripts()
	{
		_files.Add("cube.gcode", "G28\n");
		await _settings.UpdateAsync(s => s with { BeforePrintGcode = "M117 Go", PauseGcode = "{% snippet 'disable_hotends' %}" }, Ct);

		await Start("cube.gcode");

		Assert.Equal(["M117 Go"], _session.StartedWith!.Scripts!.BeforePrint);
		Assert.Equal(["M104 S0"], _session.StartedWith.Scripts.Pause);
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
