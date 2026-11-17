using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs.Enums;

using static PrintDeck.Application.Tests.TestDoubles.Eventually;

namespace PrintDeck.Application.Tests.Printer;

/// <summary>Feed rate (M220) and flow (M221) as the session tracks them, against a scripted printer.</summary>
public class PrinterSessionRateTests : IAsyncDisposable
{
	private readonly ScriptedTransport _printer = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private readonly PrinterSession _session;

	public PrinterSessionRateTests()
	{
		_session = new PrinterSession(_printer, _events.Hub, TimeProvider.System, NullLogger<PrinterSession>.Instance);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task ConnectAsync()
	{
		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		Assert.True(r.IsSuccess, r.Error);
		await Until(() => _printer.Commands.Contains("M20 L"));
	}

	[Fact]
	public async Task Asks_for_both_factors_when_connecting()
	{
		await ConnectAsync();

		List<string> sent = _printer.Commands.ToList();
		Assert.True(sent.IndexOf("M220") < sent.IndexOf("M20 L"));
		Assert.True(sent.IndexOf("M221") < sent.IndexOf("M20 L"));
		Assert.Equal((100, 100), (_session.FeedRate, _session.FlowRate));
	}

	[Fact]
	public async Task Follows_the_printers_reports()
	{
		_printer.Respond = line => ScriptedTransport.CommandOf(line) switch
		{
			"M220" => ["FR:80%", "ok"],
			"M221" => ["echo:E0 Flow: 105%", "ok"],
			_ => ["ok"]
		};

		await ConnectAsync();

		await Until(() => (_session.FeedRate, _session.FlowRate) == (80, 105));
		Assert.Contains(PrinterChange.State, _events.Published);
	}

	[Fact]
	public async Task Follows_commands_from_the_terminal_and_the_file()
	{
		await ConnectAsync();

		_session.Enqueue(["M220 S150"]);
		await Until(() => _session.FeedRate == 150);

		byte[] gcode = Encoding.UTF8.GetBytes("G1 X1\nM221 S95 ; slicer flow\nG1 X2\n");
		_session.StartJob("a.gcode", new MemoryStream(gcode), gcode.Length, null);
		await Until(() => _session.CurrentJob!.Status == JobStatus.Completed);
		Assert.Equal((150, 95), (_session.FeedRate, _session.FlowRate));
	}

	[Fact]
	public async Task Starts_over_at_100_on_a_new_connection()
	{
		await ConnectAsync();
		_session.Enqueue(["M220 S150", "M221 S90"]);
		await Until(() => _session.FlowRate == 90);

		await _session.DisconnectAsync();
		await ConnectAsync();

		Assert.Equal((100, 100), (_session.FeedRate, _session.FlowRate));
	}
}
