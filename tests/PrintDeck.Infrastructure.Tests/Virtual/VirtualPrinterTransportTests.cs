using System.Threading.Channels;

using PrintDeck.Domain.Protocol;
using PrintDeck.Domain.Temperatures;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Virtual;

/// <summary>The simulated Marlin firmware, talked to directly over its line interface.</summary>
public sealed class VirtualPrinterTransportTests : IAsyncLifetime
{
	private readonly VirtualPrinterTransport _printer = new(TimeProvider.System, speedFactor: 100);
	private readonly Channel<string> _received = Channel.CreateUnbounded<string>();
	private readonly CancellationTokenSource _stop = new();
	private Task _reader = Task.CompletedTask;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public async ValueTask InitializeAsync()
	{
		await _printer.OpenAsync(Ct);
		_reader = Task.Run(async () =>
		{
			await foreach (string line in _printer.ReadLinesAsync(_stop.Token)) await _received.Writer.WriteAsync(line);
		});
		await ReadUntil(l => l == "start");
	}

	public async ValueTask DisposeAsync()
	{
		await _stop.CancelAsync();
		await _printer.DisposeAsync();
		try { await _reader; } catch (OperationCanceledException) { }
		_stop.Dispose();
	}

	/// <summary>Reads lines until one matches; returns every line read, the match last.</summary>
	private async Task<List<string>> ReadUntil(Func<string, bool> match, int seconds = 5)
	{
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
		List<string> lines = [];
		while (true)
		{
			string line = await _received.Reader.ReadAsync(timeout.Token);
			lines.Add(line);
			if (match(line)) return lines;
		}
	}

	private Task Send(string line) => _printer.WriteLineAsync(line, Ct);

	private async Task<List<string>> SendNumbered(long n, string command)
	{
		await Send(GcodeLine.WithChecksum(n, command));
		return await ReadUntil(l => l.StartsWith("ok", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Answers_plain_commands_with_ok()
	{
		await Send("M114");
		List<string> lines = await ReadUntil(l => l == "ok");
		Assert.Contains(lines, l => l.StartsWith("X:0.00 Y:0.00 Z:0.00", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Accepts_numbered_lines_in_sequence()
	{
		await SendNumbered(0, "M110 N0");
		List<string> lines = await SendNumbered(1, "M114");
		Assert.DoesNotContain(lines, l => l.StartsWith("Resend", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Asks_for_a_resend_on_a_bad_checksum()
	{
		await SendNumbered(0, "M110 N0");
		await Send("N1 M114*0");

		List<string> lines = await ReadUntil(l => l == "ok");

		Assert.Contains(lines, l => l.StartsWith("Error:checksum mismatch", StringComparison.Ordinal));
		Assert.Contains("Resend: 1", lines);
	}

	[Fact]
	public async Task Asks_for_a_resend_when_a_line_is_skipped()
	{
		await SendNumbered(0, "M110 N0");
		List<string> lines = await SendNumbered(5, "M114");
		Assert.Contains("Resend: 1", lines);
	}

	[Fact]
	public async Task Reports_temperatures_the_host_can_parse()
	{
		await Send("M104 S200");
		await ReadUntil(l => l == "ok");
		await Send("M105");

		string report = (await ReadUntil(l => l.StartsWith("ok T:", StringComparison.Ordinal)))[^1];
		IReadOnlyList<HeaterReading> heaters = TemperatureReportParser.TryParse(report)!;

		Assert.Equal(200, heaters.Single(h => h.Heater == "T0").Target);
		Assert.Contains(heaters, h => h.IsBed);
	}

	[Fact]
	public async Task Identifies_as_marlin()
	{
		await Send("M115");
		List<string> lines = await ReadUntil(l => l == "ok");
		Assert.Contains(lines, l => l.StartsWith("FIRMWARE_NAME:Marlin", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Auto_reports_temperatures_after_M155()
	{
		await Send("M155 S1");
		await ReadUntil(l => l.StartsWith(" T:", StringComparison.Ordinal), seconds: 5);
	}

	[Fact]
	public async Task Halts_on_emergency_stop()
	{
		await Send("M112");
		await ReadUntil(l => l == "Error:Printer halted. kill() called!");
	}
}
