using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer.Enums;

using static PrintDeck.Application.Tests.TestDoubles.Eventually;

namespace PrintDeck.Application.Tests.Printer;

/// <summary>
/// Waiting for "ok": timeouts are measured on the session's <see cref="TimeProvider"/>, so a manual clock
/// makes them instant. The session re-checks every 250 ms of real time.
/// </summary>
public sealed class PrinterSessionTimeoutTests : IAsyncDisposable
{
	private static readonly TimeSpan _check = TimeSpan.FromMilliseconds(600); // > two 250 ms check rounds

	private readonly ScriptedTransport _printer = new();
	private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
	private readonly PrinterSession _session;

	public PrinterSessionTimeoutTests()
	{
		_session = new PrinterSession(_printer, new PrinterEventHub(), _clock, NullLogger<PrinterSession>.Instance);
	}

	public async ValueTask DisposeAsync() => await _session.DisposeAsync();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	/// <summary>Printer answers everything except <paramref name="silent"/>.</summary>
	private async Task ConnectIgnoring(params string[] silent)
	{
		_printer.Respond = line => silent.Contains(ScriptedTransport.CommandOf(line)) ? [] : ["ok"];
		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		Assert.True(r.IsSuccess, r.Error);
		await Until(() => _printer.Commands.Contains("M155 S2"));
	}

	[Fact]
	public async Task Connect_fails_after_three_unanswered_handshakes()
	{
		_printer.Respond = _ => [];
		using CancellationTokenSource stop = new();
		Task ticking = Task.Run(async () =>
		{
			while (!stop.IsCancellationRequested)
			{
				_clock.Advance(TimeSpan.FromSeconds(2));
				await Task.Delay(50, Ct);
			}
		}, Ct);

		Result r = await _session.ConnectAsync("SCRIPTED", 115200, Ct);
		await stop.CancelAsync();
		await ticking;

		Assert.Equal("No response from printer (check port and baud rate).", r.Error);
		Assert.Equal(PrinterState.Error, _session.State);
		Assert.Equal(3, _printer.Commands.Count(c => c == "M110 N0"));
		Assert.True(_printer.Disposed);
	}

	[Fact]
	public async Task Moves_on_when_a_command_gets_no_ok()
	{
		await ConnectIgnoring("M114");
		_session.Enqueue(["M114", "M84"]);
		await Until(() => _printer.Commands.Contains("M114"));

		_clock.Advance(TimeSpan.FromSeconds(9));
		await Task.Delay(_check, Ct);
		Assert.DoesNotContain("M84", _printer.Commands);

		_clock.Advance(TimeSpan.FromSeconds(2));
		await Until(() => _printer.Commands.Contains("M84"));
		Assert.Contains(_session.Terminal(100), e => e.Text == "No 'ok' for M114 within 10s, continuing");
	}

	[Fact]
	public async Task Long_running_commands_get_ten_minutes()
	{
		await ConnectIgnoring("M109 S200");
		_session.Enqueue(["M109 S200", "M84"]);
		await Until(() => _printer.Commands.Contains("M109 S200"));

		_clock.Advance(TimeSpan.FromMinutes(9));
		await Task.Delay(_check, Ct);
		Assert.DoesNotContain("M84", _printer.Commands);

		_clock.Advance(TimeSpan.FromMinutes(2));
		await Until(() => _printer.Commands.Contains("M84"));
	}

	[Fact]
	public async Task Busy_messages_keep_the_command_alive()
	{
		await ConnectIgnoring("M114");
		_session.Enqueue(["M114", "M84"]);
		await Until(() => _printer.Commands.Contains("M114"));

		for (int i = 0; i < 3; i++)
		{
			_clock.Advance(TimeSpan.FromSeconds(6)); // 18 s in total, but never 10 s of silence
			_printer.Emit("echo:busy: processing");
			await Task.Delay(_check, Ct);
		}
		Assert.DoesNotContain("M84", _printer.Commands);

		_printer.Emit("ok");
		await Until(() => _printer.Commands.Contains("M84"));
	}
}
