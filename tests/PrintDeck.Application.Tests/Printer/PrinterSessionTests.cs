using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Protocol;
using PrintDeck.Domain.Terminal.Enums;

using static PrintDeck.Application.Tests.TestDoubles.Eventually;

namespace PrintDeck.Application.Tests.Printer;

/// <summary>Host protocol behaviour against a scripted printer that answers instantly.</summary>
public class PrinterSessionTests : IAsyncDisposable
{
	private readonly ScriptedTransport _printer = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private readonly PrinterSession _session;

	public PrinterSessionTests()
	{
		_session = new PrinterSession(_printer, _events.Hub, TimeProvider.System, NullLogger<PrinterSession>.Instance);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task ConnectAsync()
	{
		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		Assert.True(r.IsSuccess, r.Error);
	}

	private int IndexOf(string command) => _printer.Commands.ToList().IndexOf(command);

	private static MemoryStream Gcode(string text, out long size)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		size = bytes.Length;
		return new MemoryStream(bytes);
	}

	public class Connecting : PrinterSessionTests
	{
		[Fact]
		public async Task Handshakes_with_line_number_reset_then_queries_firmware()
		{
			await ConnectAsync();

			Assert.Equal(PrinterState.Operational, _session.State);
			Assert.Equal(("SCRIPTED", 115200), (_session.Port, _session.BaudRate));
			Assert.Equal(GcodeLine.WithChecksum(0, "M110 N0"), _printer.Written[0]);
			await Until(() => _printer.Written.Count >= 3);
			Assert.Equal(GcodeLine.WithChecksum(1, "M115"), _printer.Written[1]);
			Assert.Equal(GcodeLine.WithChecksum(2, "M155 S2"), _printer.Written[2]);
		}

		[Fact]
		public async Task Refuses_a_second_connect()
		{
			await ConnectAsync();
			Assert.Equal("Already connected.", (await _session.ConnectAsync("SCRIPTED", 115200, Ct)).Error);
		}

		[Fact]
		public async Task Reads_the_firmware_name()
		{
			_printer.Respond = line => ScriptedTransport.CommandOf(line) == "M115"
				? ["FIRMWARE_NAME:Marlin 2.1.2 (Github) SOURCE_CODE_URL:github.com/MarlinFirmware/Marlin", "ok"]
				: ["ok"];

			await ConnectAsync();

			await Until(() => _session.Firmware is not null);
			Assert.Equal("Marlin 2.1.2 (Github)", _session.Firmware);
		}

		[Fact]
		public async Task Records_temperature_reports()
		{
			await ConnectAsync();
			_printer.Emit(" T:201.5 /210.0 B:60.0 /60.0 @:0 B@:0");

			await Until(() => _session.Temperatures.Latest is not null);
			Assert.Equal(201.5, _session.Temperatures.Latest!.Heaters.Single(h => h.Heater == "T0").Actual);
			Assert.Contains(PrinterChange.Temperature, _events.Published);
		}

		[Fact]
		public async Task Disconnect_goes_offline_and_closes_the_port()
		{
			await ConnectAsync();
			await _session.DisconnectAsync();

			Assert.Equal(PrinterState.Offline, _session.State);
			Assert.True(_printer.Disposed);
			Assert.Equal("Printer is not connected.", _session.Enqueue(["G28"]).Error);
		}
	}

	public class Failures : PrinterSessionTests
	{
		[Fact]
		public async Task A_halted_printer_puts_the_session_in_error()
		{
			await ConnectAsync();
			_printer.Emit("Error:Printer halted. kill() called!");

			await Until(() => _session.State == PrinterState.Error);
			Assert.Equal("Error:Printer halted. kill() called!", _session.ErrorMessage);
			Assert.Contains(_session.Terminal(100), e => e.Direction == TerminalDirection.Error);
		}

		[Fact]
		public async Task Losing_the_connection_fails_the_running_job()
		{
			await ConnectAsync();
			_printer.Respond = _ => []; // printer stops answering, so the job stays active
			Assert.True(_session.StartJob("a.gcode", Gcode("G1 X1\nG1 X2\n", out long size), size, null).IsSuccess);

			_printer.Close();

			await Until(() => _session.State == PrinterState.Error);
			Assert.StartsWith("Connection lost", _session.ErrorMessage);
			Assert.Equal(JobStatus.Failed, _session.CurrentJob!.Status);
		}

		[Fact]
		public async Task Can_reconnect_after_an_error()
		{
			await ConnectAsync();
			_printer.Emit("Error:Printer halted. kill() called!");
			await Until(() => _session.State == PrinterState.Error);

			await ConnectAsync();
			Assert.Equal(PrinterState.Operational, _session.State);
			Assert.Null(_session.ErrorMessage);
		}
	}

	public class Commands : PrinterSessionTests
	{
		[Fact]
		public void Enqueue_needs_a_connection()
		{
			Assert.Equal("Printer is not connected.", _session.Enqueue(["G28"]).Error);
		}

		[Fact]
		public async Task Sends_numbered_lines_without_comments()
		{
			await ConnectAsync();
			_session.Enqueue(["; only a comment", "", "G28 ; home", "M105"]);

			await Until(() => _printer.Commands.Contains("M105"));
			string[] sent = _printer.Commands.SkipWhile(c => c != "G28").ToArray();
			Assert.Equal(["G28", "M105"], sent);
			Assert.All(_printer.Written, w => Assert.Matches(@"^N\d+ .+\*\d+$", w));
		}

		[Fact]
		public async Task Resends_from_the_line_the_printer_asks_for()
		{
			bool rejected = false;
			_printer.Respond = line =>
			{
				if (ScriptedTransport.CommandOf(line) == "G1 X1" && !rejected)
				{
					rejected = true;
					string n = line[1..line.IndexOf(' ')];
					return [$"Error:checksum mismatch, Last Line: {long.Parse(n) - 1}", $"Resend: {n}", "ok"];
				}
				return ["ok"];
			};
			await ConnectAsync();

			_session.Enqueue(["G1 X1", "G1 X2"]);

			await Until(() => _printer.Commands.Contains("G1 X2"));
			string[] wires = _printer.Written.Where(w => ScriptedTransport.CommandOf(w) == "G1 X1").ToArray();
			Assert.Equal(2, wires.Length);
			Assert.Equal(wires[0], wires[1]); // same line number and checksum
		}

		[Fact]
		public async Task Clear_terminal_empties_the_log()
		{
			await ConnectAsync();
			Assert.NotEmpty(_session.Terminal(100));

			_session.ClearTerminal();

			Assert.Empty(_session.Terminal(100));
			Assert.Contains(PrinterChange.Terminal, _events.Published);
		}
	}

	public class Jobs : PrinterSessionTests
	{
		[Fact]
		public async Task Streams_a_file_to_completion()
		{
			await ConnectAsync();

			Result start = _session.StartJob("cube.gcode", Gcode("; header\nG28\n\nG1 X10 ; move\nG1 X20\n", out long size), size, TimeSpan.FromMinutes(1));

			Assert.True(start.IsSuccess, start.Error);
			await Until(() => _session.CurrentJob!.Status == JobStatus.Completed);
			Assert.Equal(PrinterState.Operational, _session.State);
			Assert.Equal(1, _session.CurrentJob!.Progress);
			Assert.Equal(TimeSpan.FromMinutes(1), _session.CurrentJob.SlicerEstimate);
			Assert.Equal(["G28", "G1 X10", "G1 X20"], _printer.Commands.Where(c => c is "G28" || c.StartsWith("G1")));
			Assert.Contains(PrinterChange.Job | PrinterChange.State, _events.Published);
		}

		[Fact]
		public async Task Shows_progress_and_layers_on_the_printer_screen()
		{
			await ConnectAsync();
			string gcode = ";LAYER:0\nG1 X1\n;LAYER:1\nG1 X2\n";

			_session.StartJob("cube.gcode", Gcode(gcode, out long size), size, TimeSpan.FromMinutes(10), new PrintOptions(true, 2));

			await Until(() => _session.CurrentJob!.Status == JobStatus.Completed);
			await Until(() => _printer.Commands.Contains("M117 Print done"));
			string[] sent = _printer.Commands.SkipWhile(c => c != "M73 P0").ToArray();
			Assert.Equal("M117 cube", sent[1]);
			Assert.True(Array.IndexOf(sent, "M117 Layer 1/2") < Array.IndexOf(sent, "G1 X1"));
			Assert.True(Array.IndexOf(sent, "M117 Layer 2/2") < Array.IndexOf(sent, "G1 X2"));
			Assert.Contains(sent, c => c.StartsWith("M73 P", StringComparison.Ordinal) && c.Contains(" R", StringComparison.Ordinal));
			Assert.True(Array.IndexOf(sent, "M73 P100 R0") < Array.IndexOf(sent, "M117 Print done"));
		}

		[Fact]
		public async Task Sends_nothing_to_the_screen_unless_asked()
		{
			await ConnectAsync();
			_session.StartJob("cube.gcode", Gcode(";LAYER:0\nG1 X1\n", out long size), size, null);

			await Until(() => _session.CurrentJob!.Status == JobStatus.Completed);
			await Task.Delay(100, Ct);
			Assert.DoesNotContain(_printer.Commands, c => c.StartsWith("M73", StringComparison.Ordinal) || c.StartsWith("M117", StringComparison.Ordinal));
		}

		[Fact]
		public async Task Pause_and_cancel_show_on_the_printer_screen()
		{
			await ConnectAsync();
			_printer.Respond = _ => [];
			_session.StartJob("a.gcode", Gcode("G1 X1\nG1 X2\n", out long size), size, null, new PrintOptions(true));
			_printer.Respond = _ => ["ok"];
			_printer.Emit("ok");

			_session.PauseJob();
			await Until(() => _printer.Commands.Contains("M117 Paused"));
			_session.CancelJob(["M104 S0"]);

			await Until(() => _printer.Commands.Contains("M117 Print cancelled"));
			Assert.True(IndexOf("M104 S0") < IndexOf("M117 Print cancelled"));
		}

		[Fact]
		public void Start_needs_an_idle_printer()
		{
			MemoryStream content = Gcode("G28\n", out long size);
			Assert.Equal("Printer is not ready.", _session.StartJob("a.gcode", content, size, null).Error);
			Assert.False(content.CanRead); // disposed
		}

		[Fact]
		public async Task Refuses_a_second_job_while_printing()
		{
			await ConnectAsync();
			_printer.Respond = _ => [];
			Assert.True(_session.StartJob("a.gcode", Gcode("G1 X1\n", out long size), size, null).IsSuccess);

			Assert.Equal("A print is already running.", _session.StartJob("b.gcode", Gcode("G1 X1\n", out size), size, null).Error);
		}

		[Fact]
		public void Pause_resume_and_cancel_need_a_job()
		{
			Assert.Equal("Nothing is printing.", _session.PauseJob().Error);
			Assert.Equal("Print is not paused.", _session.ResumeJob().Error);
			Assert.Equal("No active print.", _session.CancelJob([]).Error);
		}

		[Fact]
		public async Task Pause_resume_and_cancel_switch_state()
		{
			await ConnectAsync();
			_printer.Respond = _ => [];
			_session.StartJob("a.gcode", Gcode("G1 X1\nG1 X2\nG1 X3\n", out long size), size, null);

			Assert.True(_session.PauseJob().IsSuccess);
			Assert.Equal(PrinterState.Paused, _session.State);
			Assert.Equal("Nothing is printing.", _session.PauseJob().Error);

			Assert.True(_session.ResumeJob().IsSuccess);
			Assert.Equal(PrinterState.Printing, _session.State);
			Assert.True(_session.CancelJob(["M104 S0"]).IsSuccess);
			Assert.Equal(PrinterState.Operational, _session.State);
			Assert.Equal(JobStatus.Cancelled, _session.CurrentJob!.Status);
		}

		[Fact]
		public async Task Cancel_sends_the_cancel_gcode()
		{
			await ConnectAsync();
			_session.StartJob("a.gcode", Gcode(string.Concat(Enumerable.Repeat("G4 P0\n", 1000)), out long size), size, null);

			_session.CancelJob(["M104 S0", "M140 S0"]);

			await Until(() => _printer.Commands.Contains("M140 S0"));
			Assert.True(_printer.Commands.Count(c => c == "G4 P0") < 1000);
		}

		[Fact]
		public async Task Disconnect_fails_the_running_job()
		{
			await ConnectAsync();
			_printer.Respond = _ => [];
			_session.StartJob("a.gcode", Gcode("G1 X1\n", out long size), size, null);

			await _session.DisconnectAsync();

			Assert.Equal(JobStatus.Failed, _session.CurrentJob!.Status);
		}
	}
}
