using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.SdCard.Enums;

using static PrintDeck.Application.Tests.TestDoubles.Eventually;

namespace PrintDeck.Application.Tests.Printer;

/// <summary>Before/after print, pause and resume scripts, against a scripted printer.</summary>
public class PrinterSessionScriptTests : IAsyncDisposable
{
	private static readonly PrintScripts _scripts = new(
		BeforePrint: ["M117 before", "; a comment", ""],
		AfterPrint: ["M117 after"],
		Cancel: [],
		Pause: ["M117 pause"],
		Resume: ["M117 resume"]);

	private readonly ScriptedTransport _printer = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private readonly PrinterSession _session;

	public PrinterSessionScriptTests()
	{
		_printer.Respond = Answer;
		_session = new PrinterSession(_printer, _events.Hub, TimeProvider.System, NullLogger<PrinterSession>.Instance);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static IEnumerable<string> Answer(string line) => ScriptedTransport.CommandOf(line).StartsWith("M20", StringComparison.Ordinal)
		? ["Begin file list", "CUBE.GCO 100 cube.gcode", "End file list", "ok"]
		: ["ok"];

	private async Task ConnectAsync()
	{
		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		Assert.True(r.IsSuccess, r.Error);
		await Until(() => _session.SdCard == SdCardState.Ready && _session.SdFiles.Count > 0);
	}

	private int IndexOf(string command) => _printer.Commands.ToList().IndexOf(command);

	private Result Start(string gcode)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(gcode);
		return _session.StartJob("cube.gcode", new MemoryStream(bytes), bytes.Length, null, new PrintOptions(Scripts: _scripts));
	}

	[Fact]
	public async Task Sends_the_before_and_after_scripts_around_the_file()
	{
		await ConnectAsync();

		Assert.True(Start("G1 X1\nG1 X2\n").IsSuccess);

		await Until(() => _printer.Commands.Contains("M117 after"));
		Assert.True(IndexOf("M117 before") < IndexOf("G1 X1"));
		Assert.True(IndexOf("G1 X2") < IndexOf("M117 after"));
		Assert.DoesNotContain("; a comment", _printer.Commands);
		Assert.Equal(JobStatus.Completed, _session.CurrentJob!.Status);
	}

	[Fact]
	public async Task Sends_the_pause_script_and_the_resume_script_before_the_file_continues()
	{
		await ConnectAsync();
		_printer.Respond = _ => []; // hold the first line, so the pause comes before the file

		Start("G1 X1\nG1 X2\n");
		Assert.True(_session.PauseJob().IsSuccess);
		Assert.True(_session.ResumeJob().IsSuccess);
		_printer.Respond = Answer;
		_printer.Emit("ok");

		await Until(() => _printer.Commands.Contains("M117 after"));
		Assert.True(IndexOf("M117 pause") < IndexOf("M117 resume"));
		Assert.True(IndexOf("M117 resume") < IndexOf("G1 X1"));
	}

	[Fact]
	public async Task The_printers_pause_action_runs_the_pause_script_but_a_pause_it_did_itself_does_not()
	{
		await ConnectAsync();
		_printer.Respond = _ => [];
		Start("G1 X1\nG1 X2\n");

		_printer.Emit("//action:paused");
		await Until(() => _session.State == PrinterState.Paused);
		_printer.Emit("//action:resumed");
		await Until(() => _session.State == PrinterState.Printing);
		_printer.Emit("//action:pause");
		await Until(() => _session.State == PrinterState.Paused);
		_printer.Respond = Answer;
		_printer.Emit("ok");

		await Until(() => _printer.Commands.Contains("M117 pause"));
		Assert.Equal(1, _printer.Commands.Count(c => c == "M117 pause"));
		Assert.DoesNotContain("M117 resume", _printer.Commands);
	}

	[Fact]
	public async Task A_cancelled_print_skips_the_after_script()
	{
		await ConnectAsync();
		_printer.Respond = _ => [];
		Start("G1 X1\nG1 X2\n");

		Assert.True(_session.CancelJob(["M84"]).IsSuccess);
		_printer.Respond = Answer;
		_printer.Emit("ok");

		await Until(() => _printer.Commands.Contains("M84"));
		await Task.Delay(100, Ct);
		Assert.DoesNotContain("M117 after", _printer.Commands);
	}

	[Fact]
	public async Task Sd_prints_run_the_scripts_around_the_firmwares_commands()
	{
		await ConnectAsync();

		Assert.True(_session.StartSdPrint("CUBE.GCO", _scripts).IsSuccess);
		await Until(() => _printer.Commands.Contains("M24"));
		Assert.True(_session.PauseJob().IsSuccess);
		await Until(() => _printer.Commands.Contains("M117 pause"));
		Assert.True(_session.ResumeJob().IsSuccess);
		await Until(() => _printer.Commands.Count(c => c == "M24") == 2);
		_printer.Emit("Done printing file");
		await Until(() => _printer.Commands.Contains("M117 after"));

		List<string> sent = _printer.Commands.ToList();
		Assert.True(sent.IndexOf("M117 before") < sent.IndexOf("M23 CUBE.GCO"));
		Assert.True(sent.IndexOf("M25") < sent.IndexOf("M117 pause"));
		Assert.True(sent.IndexOf("M117 resume") < sent.LastIndexOf("M24"));
		Assert.True(sent.IndexOf("M27 S0") < sent.IndexOf("M117 after"));
	}
}
