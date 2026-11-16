using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Printer;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.SdCard.Enums;
using PrintDeck.Domain.Terminal.Enums;
using PrintDeck.Infrastructure.Serial;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Virtual;

/// <summary>End-to-end: the real session talking to the in-process virtual printer.</summary>
public sealed class PrinterSessionWithVirtualPrinterTests : IAsyncLifetime
{
	private readonly PrinterSession _session = new(
		new TransportFactory(TimeProvider.System), new PrinterEventHub(), TimeProvider.System, NullLogger<PrinterSession>.Instance);

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public async ValueTask InitializeAsync()
	{
		Result result = await _session.ConnectAsync(VirtualPrinterTransport.PortName, 115200, Ct);
		Assert.True(result.IsSuccess, result.Error);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	private static async Task Until(Func<bool> condition, int seconds = 10)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
		while (!condition())
		{
			if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition not met in time.");
			await Task.Delay(50, Ct);
		}
	}

	private Result Start(string name, string gcode)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(gcode);
		return _session.StartJob(name, new MemoryStream(bytes), bytes.Length, null);
	}

	[Fact]
	public async Task Connects_and_reads_firmware_and_temperatures()
	{
		Assert.Equal(PrinterState.Operational, _session.State);
		await Until(() => _session.Firmware is not null && _session.Temperatures.Latest is not null);
		Assert.StartsWith("Marlin", _session.Firmware);
		Assert.Contains(_session.Temperatures.Latest!.Heaters, h => h.IsBed);
	}

	[Fact]
	public async Task Setting_a_target_shows_up_in_reports()
	{
		_session.Enqueue(["M104 S200"]);
		await Until(() => _session.Temperatures.Latest?.Heaters.Any(h => h.Heater == "T0" && h.Target == 200) == true);
	}

	[Fact]
	public async Task Prints_a_file_to_completion()
	{
		string gcode = "; test\nG28\nG90\n" + string.Concat(Enumerable.Range(0, 50).Select(i => $"G1 X{i} Y{i} F12000 ; move {i}\n"));

		Result start = Start("test.gcode", gcode);
		Assert.True(start.IsSuccess, start.Error);
		Assert.Equal(PrinterState.Printing, _session.State);

		await Until(() => _session.CurrentJob!.Status == JobStatus.Completed, 20);
		Assert.Equal(PrinterState.Operational, _session.State);
		Assert.Equal(1, _session.CurrentJob!.Progress);
		Assert.DoesNotContain(_session.Terminal(2000), e => e.Direction == TerminalDirection.Error);
	}

	[Fact]
	public async Task Pause_stops_streaming_and_resume_continues()
	{
		Start("slow.gcode", string.Concat(Enumerable.Range(0, 400).Select(i => $"G1 X{i % 200} F600\n")));

		await Until(() => _session.CurrentJob!.LinesSent > 3);
		Assert.True(_session.PauseJob().IsSuccess);
		await Task.Delay(300, Ct);
		long frozen = _session.CurrentJob!.LinesSent;
		await Task.Delay(500, Ct);
		Assert.InRange(_session.CurrentJob.LinesSent, frozen, frozen + 1);

		Assert.True(_session.ResumeJob().IsSuccess);
		await Until(() => _session.CurrentJob.LinesSent > frozen + 2);

		Assert.True(_session.CancelJob(["M104 S0"]).IsSuccess);
		Assert.Equal(JobStatus.Cancelled, _session.CurrentJob.Status);
		Assert.Equal(PrinterState.Operational, _session.State);
	}

	[Fact]
	public void Refuses_second_job_while_printing()
	{
		Assert.True(Start("a.gcode", "G4 P5000\n").IsSuccess);
		Assert.False(Start("b.gcode", "G4 P5000\n").IsSuccess);
	}

	[Fact]
	public async Task Copies_a_file_to_the_sd_card_and_prints_it_from_there()
	{
		await Until(() => _session.SdCard == SdCardState.Ready && _session.SdFiles.Count > 0);
		byte[] gcode = Encoding.UTF8.GetBytes("; tiny\nG1 X1\nG1 X2\n");

		Result<string> upload = _session.StartSdUpload("tiny part.gcode", new MemoryStream(gcode), gcode.Length);
		Assert.True(upload.IsSuccess, upload.Error);
		Assert.Equal("TINYPART.GCO", upload.Value);

		await Until(() => _session.SdFiles.Any(f => f.Path == "TINYPART.GCO"));
		Assert.Equal(JobStatus.Completed, _session.CurrentJob!.Status);
		Assert.Equal("tiny part.gcode", _session.SdFiles.Single(f => f.Path == "TINYPART.GCO").DisplayName);

		Result print = _session.StartSdPrint("TINYPART.GCO");
		Assert.True(print.IsSuccess, print.Error);
		await Until(() => _session.CurrentJob!.Status == JobStatus.Completed);
		Assert.Equal(PrinterState.Operational, _session.State);
		Assert.Equal(1, _session.CurrentJob.Progress);
	}

	[Fact]
	public async Task A_filament_change_mid_print_is_answered_from_the_host()
	{
		Assert.True(Start("change.gcode", "G1 X1\nM600\nG1 X2\nG1 X3\n").IsSuccess);

		await Until(() => _session.Prompt is not null);
		Assert.Equal("Load new filament", _session.Prompt!.Message);
		Assert.Equal(PrinterState.Paused, _session.State); // the printer said "paused"
		await Until(() => _session.WaitingForUser);

		Assert.True(_session.AnswerPrompt(0).IsSuccess);

		await Until(() => _session.CurrentJob!.Status == JobStatus.Completed);
		Assert.Null(_session.Prompt);
		Assert.False(_session.WaitingForUser);
		Assert.Equal(PrinterState.Operational, _session.State);
	}

	[Fact]
	public async Task Notifications_from_gcode_reach_the_session()
	{
		_session.Enqueue(["M118 //action:notification Bed is level"]);
		await Until(() => _session.Notification?.Text == "Bed is level");
	}

	[Fact]
	public async Task Emergency_stop_puts_the_session_in_error()
	{
		_session.Enqueue(["M112"]);
		await Until(() => _session.State == PrinterState.Error);
		Assert.Contains("halted", _session.ErrorMessage);
	}
}
