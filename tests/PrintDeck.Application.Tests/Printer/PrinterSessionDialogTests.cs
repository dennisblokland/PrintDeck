using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

using static PrintDeck.Application.Tests.TestDoubles.Eventually;

namespace PrintDeck.Application.Tests.Printer;

/// <summary>Host action commands, firmware prompts and waits for the user, against a scripted printer.</summary>
public class PrinterSessionDialogTests : IAsyncDisposable
{
	private readonly ScriptedTransport _printer = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private readonly FakeSettingsStore _settings = new(new HostSettings { ApiKey = "KEY", CancelGcode = "M104 S0\nM140 S0" });
	private readonly PrinterSession _session;

	public PrinterSessionDialogTests()
	{
		_session = new PrinterSession(_printer, _events.Hub, TimeProvider.System, NullLogger<PrinterSession>.Instance, _settings);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task ConnectAsync()
	{
		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		Assert.True(r.IsSuccess, r.Error);
		// The scripted printer answers as it is written to, so once the last connect command is out, its "ok" is already queued.
		await Until(() => _printer.Commands.Contains("M20 L"));
	}

	private Result StartLongJob()
	{
		byte[] bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("G4 P0\n", 5000)));
		return _session.StartJob("long.gcode", new MemoryStream(bytes), bytes.Length, null);
	}

	public class Actions : PrinterSessionDialogTests
	{
		[Fact]
		public async Task Pause_from_the_printer_stops_streaming()
		{
			await ConnectAsync();
			StartLongJob();

			_printer.Emit("// action:pause");

			await Until(() => _session.State == PrinterState.Paused);
			int sent = _printer.Commands.Count;
			await Task.Delay(200, Ct);
			Assert.InRange(_printer.Commands.Count, sent, sent + 1);
			Assert.Equal("Paused from the printer", _session.Notification!.Text);
			Assert.Contains(PrinterChange.Dialog, _events.Published);

			_printer.Emit("//action:resume");
			await Until(() => _session.State == PrinterState.Printing);
		}

		[Fact]
		public async Task Paused_and_resumed_only_follow_the_printer()
		{
			await ConnectAsync();
			_printer.Respond = line => ScriptedTransport.CommandOf(line).StartsWith("M20", StringComparison.Ordinal)
				? ["Begin file list", "CUBE.GCO 100", "End file list", "ok"]
				: ["ok"];
			_session.RefreshSdFiles();
			await Until(() => _session.SdFiles.Count == 1);
			_session.StartSdPrint("CUBE.GCO");

			_printer.Emit("//action:paused");
			await Until(() => _session.State == PrinterState.Paused);
			_printer.Emit("//action:resumed");
			await Until(() => _session.State == PrinterState.Printing);

			Assert.DoesNotContain("M25", _printer.Commands);
			Assert.Single(_printer.Commands, c => c == "M24"); // only the start
		}

		[Fact]
		public async Task Cancel_from_the_printer_runs_the_cancel_script()
		{
			await ConnectAsync();
			StartLongJob();

			_printer.Emit("//action:cancel");

			await Until(() => _session.CurrentJob!.Status == JobStatus.Cancelled);
			await Until(() => _printer.Commands.Contains("M140 S0"));
			Assert.Equal("Cancelled from the printer", _session.Notification!.Text);
		}

		[Fact]
		public async Task Notifications_and_runout_are_shown_until_dismissed()
		{
			await ConnectAsync();

			_printer.Emit("//action:notification Heating done");
			await Until(() => _session.Notification?.Text == "Heating done");

			_printer.Emit("//action:out_of_filament T0");
			await Until(() => _session.Notification?.Text == "Filament runout (T0)");

			_session.DismissNotification();
			Assert.Null(_session.Notification);
		}

		[Fact]
		public async Task Disconnect_action_goes_offline()
		{
			await ConnectAsync();
			_printer.Emit("//action:disconnect");
			await Until(() => _session.State == PrinterState.Offline);
		}

		[Fact]
		public async Task Unknown_actions_are_ignored()
		{
			await ConnectAsync();
			_printer.Emit("//action:start");
			_printer.Emit("//action:probe_failed");
			await Task.Delay(100, Ct);
			Assert.Equal(PrinterState.Operational, _session.State);
			Assert.Null(_session.Notification);
		}
	}

	public class Prompts : PrinterSessionDialogTests
	{
		private void ShowPrompt()
		{
			_printer.Emit("//action:prompt_begin Filament runout T0");
			_printer.Emit("//action:prompt_choice Purge more");
			_printer.Emit("//action:prompt_button Continue");
			_printer.Emit("//action:prompt_show");
		}

		[Fact]
		public async Task Builds_the_prompt_and_answers_with_M876()
		{
			await ConnectAsync();
			ShowPrompt();

			await Until(() => _session.Prompt is not null);
			Assert.Equal(new HostPrompt("Filament runout T0", ["Purge more", "Continue"]).Message, _session.Prompt!.Message);
			Assert.Equal(["Purge more", "Continue"], _session.Prompt.Choices);

			Assert.Equal("Not one of the choices.", _session.AnswerPrompt(2).Error);
			Assert.True(_session.AnswerPrompt(1).IsSuccess);
			Assert.Null(_session.Prompt);
			await Until(() => _printer.Written.Contains("M876 S1")); // raw: no line number, past the queue
		}

		[Fact]
		public async Task Prompt_end_closes_the_prompt()
		{
			await ConnectAsync();
			ShowPrompt();
			await Until(() => _session.Prompt is not null);

			_printer.Emit("//action:prompt_end");

			await Until(() => _session.Prompt is null);
			Assert.Equal("The printer is not asking anything.", _session.AnswerPrompt(0).Error);
		}

		[Fact]
		public async Task A_prompt_without_buttons_gets_continue()
		{
			await ConnectAsync();
			_printer.Emit("//action:prompt_begin Nozzle cleaned?");
			_printer.Emit("//action:prompt_show");

			await Until(() => _session.Prompt is not null);
			Assert.Equal(["Continue"], _session.Prompt!.Choices);
		}

		[Fact]
		public async Task Announces_prompt_support_when_the_firmware_has_it()
		{
			_printer.Respond = line => ScriptedTransport.CommandOf(line) == "M115"
				? ["FIRMWARE_NAME:Marlin 2.1.2", "Cap:PROMPT_SUPPORT:1", "ok"]
				: ["ok"];
			await ConnectAsync();
			await Until(() => _printer.Commands.Contains("M876 P1"));
		}

		[Fact]
		public async Task The_answer_does_not_release_the_blocked_command_early()
		{
			// M600 blocks until the prompt is answered; its ok comes first, then one for the out-of-band M876.
			bool answered = false;
			_printer.Respond = line =>
			{
				string command = ScriptedTransport.CommandOf(line);
				if (command == "M600") return ["//action:prompt_begin Load filament", "//action:prompt_show"];
				if (command == "M876 S0") { answered = true; return ["ok", "ok"]; } // M600 finishes, then M876 itself
				return ["ok"];
			};
			await ConnectAsync();

			_session.Enqueue(["M600", "G1 X1", "G1 X2"]);
			await Until(() => _session.Prompt is not null);
			Assert.DoesNotContain("G1 X1", _printer.Commands);

			_session.AnswerPrompt(0);

			await Until(() => _printer.Commands.Contains("G1 X2"));
			Assert.True(answered);
			string[] after = _printer.Commands.SkipWhile(c => c != "M876 S0").ToArray();
			Assert.Equal(["M876 S0", "G1 X1", "G1 X2"], after.Where(c => c is "M876 S0" or "G1 X1" or "G1 X2"));
		}
	}

	public class Waiting : PrinterSessionDialogTests
	{
		[Fact]
		public async Task Busy_paused_for_user_waits_until_the_next_ok()
		{
			await ConnectAsync();
			Assert.Equal("The printer is not waiting.", _session.ContinueFromUser().Error);

			_printer.Emit("echo:busy: paused for user");
			await Until(() => _session.WaitingForUser);

			Assert.True(_session.ContinueFromUser().IsSuccess);
			await Until(() => _printer.Written.Contains("M108"));

			_printer.Emit("ok");
			await Until(() => !_session.WaitingForUser);
		}

		[Fact]
		public async Task Disconnect_clears_dialogs()
		{
			await ConnectAsync();
			_printer.Emit("echo:busy: paused for user");
			_printer.Emit("//action:notification Hi");
			await Until(() => _session.WaitingForUser && _session.Notification is not null);

			await _session.DisconnectAsync();

			Assert.False(_session.WaitingForUser);
			Assert.Null(_session.Notification);
		}
	}

	public class SdCancel : PrinterSessionDialogTests
	{
		[Theory]
		[InlineData("Marlin 2.1.2", "M524")]
		[InlineData("Prusa-Firmware 3.13.3 based on Marlin", "M603")]
		public async Task Uses_the_firmwares_abort_command(string firmware, string abort)
		{
			_printer.Respond = line => ScriptedTransport.CommandOf(line) switch
			{
				"M115" => [$"FIRMWARE_NAME:{firmware} SOURCE_CODE_URL:x", "ok"],
				var c when c.StartsWith("M20", StringComparison.Ordinal) => ["Begin file list", "CUBE.GCO 100", "End file list", "ok"],
				_ => ["ok"]
			};
			await ConnectAsync();
			await Until(() => _session.Firmware is not null && _session.SdFiles.Count == 1);
			_session.StartSdPrint("CUBE.GCO");

			_session.CancelJob(["M104 S0"]);

			await Until(() => _printer.Commands.Contains("M104 S0"));
			Assert.Contains(abort, _printer.Commands);
			Assert.DoesNotContain(abort == "M524" ? "M603" : "M524", _printer.Commands);
		}
	}
}
