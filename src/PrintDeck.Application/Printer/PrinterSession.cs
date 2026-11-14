using System.Collections.Concurrent;
using System.Text;

using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Protocol;
using PrintDeck.Domain.Temperatures;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Printer;

/// <summary>
/// Marlin-style host protocol: one command in flight, line numbers + checksums,
/// resend handling, temperature polling and job streaming.
/// </summary>
public sealed class PrinterSession(
	IPrinterTransportFactory transports,
	PrinterEventHub events,
	TimeProvider time,
	ILogger<PrinterSession> logger) : IPrinterSession, IAsyncDisposable
{
	private static readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan _longCommandTimeout = TimeSpan.FromMinutes(10);
	private static readonly TimeSpan _temperatureInterval = TimeSpan.FromSeconds(2);

	private readonly Lock _gate = new();
	private readonly ConcurrentQueue<string> _manual = new();
	private readonly SemaphoreSlim _work = new(0);
	private readonly Dictionary<long, string> _sent = new();
	private readonly RingBuffer<TerminalEntry> _terminal = new(2000);

	private IPrinterTransport? _transport;
	private CancellationTokenSource? _cts;
	private Task _reader = Task.CompletedTask;
	private Task _sender = Task.CompletedTask;
	private Task _poller = Task.CompletedTask;
	private TaskCompletionSource _ok = new();
	private TaskCompletionSource _started = new();
	private StreamReader? _job;
	private long _nextLine;
	private long? _resendFrom;
	private long _terminalId;
	private long _lastRxTicks;
	private long _lastTempTicks;
	private int _pollPending;

	public PrinterState State { get; private set; } = PrinterState.Offline;
	public string? Port { get; private set; }
	public int BaudRate { get; private set; }
	public string? Firmware { get; private set; }
	public string? ErrorMessage { get; private set; }
	public PrintJob? CurrentJob { get; private set; }
	public TemperatureHistory Temperatures { get; } = new();

	private DateTimeOffset Now => time.GetUtcNow();

	public IReadOnlyList<TerminalEntry> Terminal(int last) => _terminal.Snapshot(last);

	public void ClearTerminal()
	{
		_terminal.Clear();
		events.Publish(PrinterChange.Terminal);
	}

	// ---------------------------------------------------------------- connection

	public async Task<Result> ConnectAsync(string port, int baudRate, CancellationToken ct)
	{
		lock (_gate)
		{
			if (State is not (PrinterState.Offline or PrinterState.Error))
				return Result.Failure("Already connected.");
			State = PrinterState.Connecting;
		}

		Port = port;
		BaudRate = baudRate;
		ErrorMessage = null;
		Firmware = null;
		Temperatures.Clear();
		events.Publish(PrinterChange.State);
		Log(TerminalDirection.Info, $"Connecting to {port} @ {baudRate}");

		try
		{
			_cts = new CancellationTokenSource();
			_transport = transports.Create(port, baudRate);
			await _transport.OpenAsync(ct);

			_started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			_reader = Task.Run(() => ReadLoopAsync(_transport, _cts.Token));

			// Most boards reset when the port opens; give the bootloader a moment.
			await Task.WhenAny(_started.Task, Task.Delay(TimeSpan.FromSeconds(3), ct));

			bool handshake = false;
			for (int attempt = 0; attempt < 3 && !handshake; attempt++)
			{
				_nextLine = 0;
				handshake = await SendAndAwaitOkAsync("M110 N0", TimeSpan.FromSeconds(5), _cts.Token);
			}
			if (!handshake) throw new TimeoutException("No response from printer (check port and baud rate).");

			_sender = Task.Run(() => SendLoopAsync(_cts.Token));
			_poller = Task.Run(() => PollLoopAsync(_cts.Token));
			Enqueue(["M115", "M155 S2"]);

			SetState(PrinterState.Operational);
			Log(TerminalDirection.Info, "Connected");
			return Result.Success();
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Connect to {Port} failed", port);
			await TeardownAsync();
			Fail(ex.Message);
			return Result.Failure(ex.Message);
		}
	}

	public async Task DisconnectAsync()
	{
		await TeardownAsync();
		lock (_gate) CurrentJob?.Fail(Now);
		ErrorMessage = null;
		SetState(PrinterState.Offline);
		Log(TerminalDirection.Info, "Disconnected");
	}

	private async Task TeardownAsync()
	{
		_cts?.Cancel();
		_ok.TrySetCanceled();
		try { await Task.WhenAll(_sender, _poller); } catch { /* cancelled */ }
		if (_transport is not null)
		{
			try { await _transport.DisposeAsync(); } catch { /* port already gone */ }
		}
		try { await _reader; } catch { /* cancelled */ }

		_transport = null;
		lock (_gate)
		{
			_job?.Dispose();
			_job = null;
			_sent.Clear();
			_resendFrom = null;
		}
		_manual.Clear();
	}

	private void Fail(string message)
	{
		ErrorMessage = message;
		lock (_gate) CurrentJob?.Fail(Now);
		Log(TerminalDirection.Error, message);
		SetState(PrinterState.Error);
	}

	// ---------------------------------------------------------------- commands & jobs

	public Result Enqueue(IEnumerable<string> commands)
	{
		if (!State.IsConnected() && State != PrinterState.Connecting)
			return Result.Failure("Printer is not connected.");

		foreach (string raw in commands)
			if (GcodeLine.Clean(raw) is { } line) _manual.Enqueue(line);

		_work.Release();
		return Result.Success();
	}

	public Result StartJob(string fileName, Stream content, long size, TimeSpan? estimate)
	{
		lock (_gate)
		{
			if (State != PrinterState.Operational)
			{
				content.Dispose();
				return Result.Failure(State.IsBusy() ? "A print is already running." : "Printer is not ready.");
			}
			_job = new StreamReader(content, Encoding.UTF8);
			CurrentJob = new PrintJob(fileName, size, estimate, Now);
			State = PrinterState.Printing;
		}
		Log(TerminalDirection.Info, $"Printing {fileName}");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		_work.Release();
		return Result.Success();
	}

	public Result PauseJob()
	{
		lock (_gate)
		{
			if (CurrentJob?.Status != JobStatus.Printing) return Result.Failure("Nothing is printing.");
			CurrentJob.Pause(Now);
			State = PrinterState.Paused;
		}
		Log(TerminalDirection.Info, "Print paused");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		return Result.Success();
	}

	public Result ResumeJob()
	{
		lock (_gate)
		{
			if (CurrentJob?.Status != JobStatus.Paused) return Result.Failure("Print is not paused.");
			CurrentJob.Resume(Now);
			State = PrinterState.Printing;
		}
		Log(TerminalDirection.Info, "Print resumed");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		_work.Release();
		return Result.Success();
	}

	public Result CancelJob(IEnumerable<string> cancelCommands)
	{
		lock (_gate)
		{
			if (CurrentJob is not { IsActive: true }) return Result.Failure("No active print.");
			CurrentJob.Cancel(Now);
			_job?.Dispose();
			_job = null;
			State = PrinterState.Operational;
		}
		Log(TerminalDirection.Info, "Print cancelled");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		return Enqueue(cancelCommands);
	}

	private string? NextCommand()
	{
		if (_manual.TryDequeue(out string? manual)) return manual;

		lock (_gate)
		{
			if (CurrentJob?.Status != JobStatus.Printing || _job is null) return null;

			while (_job.ReadLine() is { } raw)
			{
				CurrentJob.Advance(Encoding.UTF8.GetByteCount(raw) + 1);
				if (GcodeLine.Clean(raw) is { } line) return line;
			}

			CurrentJob.Complete(Now);
			_job.Dispose();
			_job = null;
			State = PrinterState.Operational;
		}
		Log(TerminalDirection.Info, $"Print finished: {CurrentJob.FileName}");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		return null;
	}

	// ---------------------------------------------------------------- loops

	private async Task SendLoopAsync(CancellationToken ct)
	{
		try
		{
			while (!ct.IsCancellationRequested)
			{
				if (TakeResend() is { } from)
				{
					for (long n = from; n < _nextLine && !ct.IsCancellationRequested; n++)
					{
						string? cmd;
						lock (_gate) _sent.TryGetValue(n, out cmd);
						if (cmd is null) { Log(TerminalDirection.Error, $"Cannot resend line {n}: not in history"); break; }
						await SendAndAwaitOkAsync(cmd, null, ct, lineNumber: n);
					}
					continue;
				}

				string? command = NextCommand();
				if (command is null)
				{
					await _work.WaitAsync(TimeSpan.FromMilliseconds(500), ct);
					continue;
				}

				await SendAndAwaitOkAsync(command, null, ct);
				if (CurrentJob?.IsActive == true) events.Publish(PrinterChange.Job);
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			logger.LogError(ex, "Send loop crashed");
			Fail($"Communication error: {ex.Message}");
		}
	}

	private long? TakeResend()
	{
		lock (_gate)
		{
			long? from = _resendFrom;
			_resendFrom = null;
			return from;
		}
	}

	/// <summary>Sends one numbered line and waits for "ok". Returns false on timeout.</summary>
	private async Task<bool> SendAndAwaitOkAsync(string command, TimeSpan? timeout, CancellationToken ct, long? lineNumber = null)
	{
		IPrinterTransport transport = _transport ?? throw new InvalidOperationException("Not connected.");
		long n = lineNumber ?? _nextLine++;
		string wire = GcodeLine.WithChecksum(n, command);

		lock (_gate)
		{
			_sent[n] = command;
			_sent.Remove(n - 500);
		}

		TaskCompletionSource ok = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_ok = ok;
		long sentAt = Now.UtcTicks;
		await transport.WriteLineAsync(wire, ct);
		Log(TerminalDirection.Sent, wire);

		TimeSpan limit = timeout ?? (GcodeLine.IsLongRunning(command) ? _longCommandTimeout : _idleTimeout);
		while (true)
		{
			Task done = await Task.WhenAny(ok.Task, Task.Delay(250, ct));
			if (done == ok.Task) return true;
			ct.ThrowIfCancellationRequested();

			// Any traffic (busy:, temperature reports) counts as a sign of life.
			long lastActivity = Math.Max(sentAt, Interlocked.Read(ref _lastRxTicks));
			if (Now.UtcTicks - lastActivity > limit.Ticks)
			{
				if (timeout is null) Log(TerminalDirection.Info, $"No 'ok' for {command} within {limit.TotalSeconds:0}s, continuing");
				return false;
			}
		}
	}

	private async Task ReadLoopAsync(IPrinterTransport transport, CancellationToken ct)
	{
		try
		{
			await foreach (string line in transport.ReadLinesAsync(ct))
				HandleLine(line);

			if (!ct.IsCancellationRequested) throw new IOException("Connection closed by printer.");
		}
		catch (OperationCanceledException) { }
		catch (Exception ex) when (!ct.IsCancellationRequested)
		{
			logger.LogWarning(ex, "Read loop ended");
			_ = Task.Run(async () =>
			{
				await TeardownAsync();
				Fail($"Connection lost: {ex.Message}");
			});
		}
	}

	internal void HandleLine(string raw)
	{
		string line = raw.Trim();
		if (line.Length == 0) return;

		Interlocked.Exchange(ref _lastRxTicks, Now.UtcTicks);
		Log(line.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ? TerminalDirection.Error : TerminalDirection.Received, line);

		if (TemperatureReportParser.TryParse(line) is { } heaters)
		{
			Temperatures.Record(new TemperatureSample(Now, heaters));
			Interlocked.Exchange(ref _lastTempTicks, Now.UtcTicks);
			Interlocked.Exchange(ref _pollPending, 0);
			events.Publish(PrinterChange.Temperature);
		}

		if (line.StartsWith("start", StringComparison.OrdinalIgnoreCase))
			_started.TrySetResult();
		else if (line.StartsWith("FIRMWARE_NAME:", StringComparison.Ordinal))
		{
			int end = line.IndexOf(" SOURCE_CODE_URL", StringComparison.Ordinal);
			Firmware = (end > 0 ? line[14..end] : line[14..]).Trim();
			events.Publish(PrinterChange.State);
		}
		else if (line.StartsWith("Resend:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("rs ", StringComparison.Ordinal))
		{
			string digits = new string(line.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
			if (long.TryParse(digits, out long n)) lock (_gate) _resendFrom = n;
		}
		else if (line.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
				 && (line.Contains("halted", StringComparison.OrdinalIgnoreCase) || line.Contains("kill", StringComparison.OrdinalIgnoreCase)))
		{
			_ = Task.Run(async () =>
			{
				await TeardownAsync();
				Fail(line);
			});
		}

		if (line.StartsWith("ok", StringComparison.Ordinal))
			_ok.TrySetResult();
	}

	private async Task PollLoopAsync(CancellationToken ct)
	{
		using PeriodicTimer timer = new(_temperatureInterval, time);
		try
		{
			while (await timer.WaitForNextTickAsync(ct))
			{
				TimeSpan sinceReport = TimeSpan.FromTicks(Now.UtcTicks - Interlocked.Read(ref _lastTempTicks));
				// Firmware with M155 auto-report needs no polling.
				if (sinceReport > _temperatureInterval * 1.5 && Interlocked.Exchange(ref _pollPending, 1) == 0)
					Enqueue(["M105"]);
			}
		}
		catch (OperationCanceledException) { }
	}

	// ---------------------------------------------------------------- helpers

	private void SetState(PrinterState state)
	{
		State = state;
		events.Publish(PrinterChange.State);
	}

	private void Log(TerminalDirection direction, string text)
	{
		_terminal.Add(new TerminalEntry(Interlocked.Increment(ref _terminalId), Now, direction, text));
		events.Publish(PrinterChange.Terminal);
	}

	public async ValueTask DisposeAsync()
	{
		await TeardownAsync();
		_cts?.Dispose();
	}
}
