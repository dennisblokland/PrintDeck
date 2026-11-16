using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.SdCard;
using PrintDeck.Domain.SdCard.Enums;

using static PrintDeck.Application.Tests.TestDoubles.Eventually;

namespace PrintDeck.Application.Tests.Printer;

/// <summary>SD card listing, printing from the card and copying files onto it, against a scripted printer.</summary>
public class PrinterSessionSdCardTests : IAsyncDisposable
{
	private readonly ScriptedTransport _printer = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private readonly PrinterSession _session;

	/// <summary>Extra replies per command word, before the "ok".</summary>
	private readonly Dictionary<string, string[]> _replies = new()
	{
		["M20"] = ["Begin file list", "CUBE.GCO 100 cube.gcode", "BENCHY~1.GCO 2000 benchy 0.2mm.gcode", "README.TXT 10", "End file list"],
		["M28"] = ["Writing to file: CUBE~1.GCO"]
	};

	public PrinterSessionSdCardTests()
	{
		_printer.Respond = line =>
		{
			string command = ScriptedTransport.CommandOf(line);
			string word = command.Split(' ')[0];
			return [.. _replies.GetValueOrDefault(word, []), "ok"];
		};
		_session = new PrinterSession(_printer, _events.Hub, TimeProvider.System, NullLogger<PrinterSession>.Instance);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task ConnectAsync()
	{
		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		Assert.True(r.IsSuccess, r.Error);
		await Until(() => _session.SdCard == SdCardState.Ready && _session.SdFiles.Count > 0);
	}

	private static MemoryStream Gcode(string text, out long size)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		size = bytes.Length;
		return new MemoryStream(bytes);
	}

	private int IndexOf(string command) => _printer.Commands.ToList().IndexOf(command);

	public class Listing : PrinterSessionSdCardTests
	{
		[Fact]
		public async Task Lists_the_card_on_connect_without_reinitialising_it()
		{
			await ConnectAsync();

			Assert.Contains("M20 L", _printer.Commands);
			Assert.DoesNotContain("M21", _printer.Commands);
			Assert.Equal(
				[new SdFile("BENCHY~1.GCO", 2000, "benchy 0.2mm.gcode"), new SdFile("CUBE.GCO", 100, "cube.gcode")],
				_session.SdFiles);
			Assert.Contains(PrinterChange.Files, _events.Published);
		}

		[Fact]
		public async Task A_card_that_comes_ready_is_listed_again()
		{
			await ConnectAsync();
			int lists = _printer.Commands.Count(c => c == "M20 L");

			_printer.Emit("echo:SD card ok");

			await Until(() => _printer.Commands.Count(c => c == "M20 L") > lists);
		}

		[Fact]
		public async Task A_missing_card_empties_the_list()
		{
			await ConnectAsync();

			_printer.Emit("echo:SD init fail");

			await Until(() => _session.SdCard == SdCardState.Unavailable);
			Assert.Empty(_session.SdFiles);
		}

		[Fact]
		public async Task Init_and_release_send_M21_and_M22()
		{
			await ConnectAsync();

			Assert.True(_session.InitSdCard().IsSuccess);
			await Until(() => _printer.Commands.Contains("M21"));

			Assert.True(_session.ReleaseSdCard().IsSuccess);
			await Until(() => _printer.Commands.Contains("M22"));
			Assert.Equal(SdCardState.Unavailable, _session.SdCard);
			Assert.Empty(_session.SdFiles);
		}

		[Fact]
		public async Task Delete_removes_the_file_and_lists_again()
		{
			await ConnectAsync();
			int lists = _printer.Commands.Count(c => c == "M20 L");

			Assert.True(_session.DeleteSdFile("CUBE.GCO").IsSuccess);

			await Until(() => _printer.Commands.Count(c => c == "M20 L") > lists);
			Assert.True(IndexOf("M30 CUBE.GCO") >= 0);
			Assert.Equal("Not a file on the SD card.", _session.DeleteSdFile("A.GCO\nG28").Error);
		}

		[Fact]
		public async Task Disconnect_forgets_the_card()
		{
			await ConnectAsync();
			await _session.DisconnectAsync();

			Assert.Equal(SdCardState.Unknown, _session.SdCard);
			Assert.Empty(_session.SdFiles);
		}
	}

	public class Printing : PrinterSessionSdCardTests
	{
		[Fact]
		public async Task Selects_starts_and_follows_progress_until_done()
		{
			await ConnectAsync();

			Result start = _session.StartSdPrint("CUBE.GCO");

			Assert.True(start.IsSuccess, start.Error);
			Assert.Equal(PrinterState.Printing, _session.State);
			Assert.Equal(JobKind.SdPrint, _session.CurrentJob!.Kind);
			Assert.Equal("cube.gcode", _session.CurrentJob.FileName);
			await Until(() => _printer.Commands.Contains("M27 S2"));
			Assert.True(IndexOf("M23 CUBE.GCO") < IndexOf("M24"));

			_printer.Emit("SD printing byte 40/100");
			await Until(() => _session.CurrentJob.Progress == 0.4);

			_printer.Emit("Done printing file");
			await Until(() => _session.CurrentJob.Status == JobStatus.Completed);
			Assert.Equal(PrinterState.Operational, _session.State);
			await Until(() => _printer.Commands.Contains("M27 S0"));
		}

		[Fact]
		public async Task Accepts_a_path_in_any_case()
		{
			await ConnectAsync();
			Assert.True(_session.StartSdPrint("cube.gco").IsSuccess);
			await Until(() => _printer.Commands.Contains("M23 CUBE.GCO"));
		}

		[Fact]
		public async Task Refuses_unknown_files_and_a_missing_card()
		{
			Assert.Equal("Printer is not ready.", _session.StartSdPrint("CUBE.GCO").Error);
			await ConnectAsync();

			Assert.Equal("NOPE.GCO is not on the SD card.", _session.StartSdPrint("NOPE.GCO").Error);
			_session.ReleaseSdCard();
			Assert.Equal("No SD card.", _session.StartSdPrint("CUBE.GCO").Error);
		}

		[Fact]
		public async Task Pause_resume_and_cancel_go_to_the_printer()
		{
			await ConnectAsync();
			_session.StartSdPrint("CUBE.GCO");

			Assert.True(_session.PauseJob().IsSuccess);
			await Until(() => _printer.Commands.Contains("M25"));
			Assert.Equal(PrinterState.Paused, _session.State);

			Assert.True(_session.ResumeJob().IsSuccess);
			await Until(() => _printer.Commands.Count(c => c == "M24") == 2);

			Assert.True(_session.CancelJob(["M104 S0"]).IsSuccess);
			await Until(() => _printer.Commands.Contains("M104 S0"));
			Assert.True(IndexOf("M524") < IndexOf("M104 S0"));
			Assert.Equal(JobStatus.Cancelled, _session.CurrentJob!.Status);
			Assert.Equal(PrinterState.Operational, _session.State);
		}

		[Fact]
		public async Task A_file_that_will_not_open_fails_the_print()
		{
			_replies["M23"] = ["echo:open failed, File: CUBE.GCO."];
			await ConnectAsync();

			_session.StartSdPrint("CUBE.GCO");

			await Until(() => _session.CurrentJob!.Status == JobStatus.Failed);
			Assert.Equal(PrinterState.Operational, _session.State);
		}

		[Fact]
		public async Task Removing_the_card_fails_the_print()
		{
			await ConnectAsync();
			_session.StartSdPrint("CUBE.GCO");

			_printer.Emit("echo:SD card released");

			await Until(() => _session.CurrentJob!.Status == JobStatus.Failed);
			Assert.Equal(SdCardState.Unavailable, _session.SdCard);
		}

		[Fact]
		public async Task The_card_cannot_be_changed_while_printing_from_it()
		{
			await ConnectAsync();
			_session.StartSdPrint("CUBE.GCO");

			Assert.Equal("This file is printing.", _session.DeleteSdFile("CUBE.GCO").Error);
			Assert.Equal("The SD card is in use.", _session.InitSdCard().Error);
			Assert.Equal("The SD card is in use.", _session.ReleaseSdCard().Error);
		}
	}

	public class Uploading : PrinterSessionSdCardTests
	{
		[Fact]
		public async Task Writes_the_file_between_M28_and_M29_then_lists_again()
		{
			await ConnectAsync();
			// The printer is asked something mid-transfer: it must wait until the file is closed.
			_printer.Respond = line =>
			{
				string command = ScriptedTransport.CommandOf(line);
				if (command.StartsWith("M28", StringComparison.Ordinal)) _session.Enqueue(["M117 hello"]);
				return [.. _replies.GetValueOrDefault(command.Split(' ')[0], []), "ok"];
			};
			int lists = _printer.Commands.Count(c => c == "M20 L");

			Result<string> started = _session.StartSdUpload("cube.gcode", Gcode("; header\nG28\nM29 ; stray\nG1 X10 ; move\n", out long size), size);

			Assert.True(started.IsSuccess, started.Error);
			Assert.Equal("CUBE~1.GCO", started.Value); // CUBE.GCO is taken
			Assert.Equal(PrinterState.Transferring, _session.State);
			Assert.Equal(JobKind.SdUpload, _session.CurrentJob!.Kind);
			await Until(() => _session.CurrentJob.Status == JobStatus.Completed);
			await Until(() => _printer.Commands.Count(c => c == "M20 L") > lists);
			Assert.Equal(PrinterState.Operational, _session.State);

			string[] commands = _printer.Commands.ToArray();
			int open = Array.IndexOf(commands, "M28 CUBE~1.GCO");
			int close = Array.IndexOf(commands, "M29");
			Assert.Equal(["G28", "G1 X10"], commands[(open + 1)..close]);
			Assert.True(Array.IndexOf(commands, "M117 hello") > close);
		}

		[Fact]
		public async Task Stops_when_the_printer_will_not_write_the_file()
		{
			_replies["M28"] = ["echo:open failed, File: CUBE~1.GCO."];
			await ConnectAsync();

			_session.StartSdUpload("cube.gcode", Gcode("G28\nG1 X10\n", out long size), size);

			await Until(() => _session.CurrentJob!.Status == JobStatus.Failed);
			Assert.Equal(PrinterState.Operational, _session.State);
			Assert.DoesNotContain("G28", _printer.Commands); // never run as commands
		}

		[Fact]
		public async Task Cancel_closes_and_deletes_the_partial_file()
		{
			await ConnectAsync();
			_printer.Respond = line => ScriptedTransport.CommandOf(line) switch
			{
				"G1 X2" => [], // printer stalls mid-file
				var c => [.. _replies.GetValueOrDefault(c.Split(' ')[0], []), "ok"]
			};
			_session.StartSdUpload("cube.gcode", Gcode("G1 X1\nG1 X2\nG1 X3\n", out long size), size);
			await Until(() => _printer.Commands.Contains("G1 X2"));

			Assert.Equal("An SD card upload cannot be paused.", _session.PauseJob().Error);
			Assert.True(_session.CancelJob(["M104 S0"]).IsSuccess);
			_printer.Respond = _ => ["ok"];
			_printer.Emit("ok");

			await Until(() => _printer.Commands.Contains("M30 CUBE~1.GCO"));
			Assert.True(IndexOf("M29") < IndexOf("M30 CUBE~1.GCO"));
			Assert.DoesNotContain("M104 S0", _printer.Commands);
			Assert.Equal(JobStatus.Cancelled, _session.CurrentJob!.Status);
		}

		[Fact]
		public async Task Needs_an_idle_printer_with_a_card()
		{
			MemoryStream content = Gcode("G28\n", out long size);
			Assert.Equal("Printer is not ready.", _session.StartSdUpload("a.gcode", content, size).Error);
			Assert.False(content.CanRead);

			await ConnectAsync();
			_session.ReleaseSdCard();
			Assert.Equal("No SD card.", _session.StartSdUpload("a.gcode", Gcode("G28\n", out size), size).Error);
		}
	}
}
