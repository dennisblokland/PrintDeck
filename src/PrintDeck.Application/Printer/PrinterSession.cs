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
using PrintDeck.Domain.SdCard;
using PrintDeck.Domain.SdCard.Enums;
using PrintDeck.Domain.Temperatures;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Printer;

/// <summary>
/// Marlin-style host protocol: one command in flight, line numbers + checksums,
/// resend handling, temperature polling and job streaming.
/// Also drives the printer's SD card: listing, printing from it and copying files onto it,
/// and follows the firmware's host action commands (pause/cancel from the LCD, prompts, notifications).
/// </summary>
public sealed class PrinterSession(
	IPrinterTransportFactory transports,
	PrinterEventHub events,
	TimeProvider time,
	ILogger<PrinterSession> logger,
	ISettingsStore? settings = null,
	ISerialLog? serialLog = null) : IPrinterSession, IAsyncDisposable
{
	private static readonly TimeSpan _idleTimeout = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan _longCommandTimeout = TimeSpan.FromMinutes(10);
	private static readonly TimeSpan _temperatureInterval = TimeSpan.FromSeconds(2);
	private static readonly TimeSpan _sdProgressInterval = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan _outOfBandOkWindow = TimeSpan.FromSeconds(30);

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
	private PrinterScreenReporter? _screen;
	private LayerCounter? _layers;
	private int _layerChanged;
	private long _nextLine;
	private long? _resendFrom;
	private long _terminalId;
	private long _lastRxTicks;
	private long _lastTempTicks;
	private int _pollPending;
	private long _lastSdProgressTicks;
	private List<SdFile>? _sdListing;
	private string? _uploadTarget;
	private bool _uploadOpened;
	private bool _uploadAccepted;

	/// <summary>Original names of files copied to the card, which Marlin only stores as 8.3 names.</summary>
	private readonly ConcurrentDictionary<string, string> _uploadedNames = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Scripts of the running print; <see cref="PrintScripts.None"/> between prints.</summary>
	private PrintScripts _scripts = PrintScripts.None;

	private string? _promptMessage;
	private List<string> _promptChoices = [];

	/// <summary>
	/// An out-of-band line (M876, M108) still gets its own "ok" once the firmware's queue reaches it.
	/// This many "ok"s pass first (the command that was in flight), then one is swallowed; -1 when none is owed.
	/// </summary>
	private int _okToSkipAfter = -1;
	private long _okSkipDeadlineTicks;

	public PrinterState State { get; private set; } = PrinterState.Offline;
	public string? Port { get; private set; }
	public int BaudRate { get; private set; }
	public string? Firmware { get; private set; }
	public string? ErrorMessage { get; private set; }
	public PrintJob? CurrentJob { get; private set; }
	public TemperatureHistory Temperatures { get; } = new();
	public SdCardState SdCard { get; private set; } = SdCardState.Unknown;
	public IReadOnlyList<SdFile> SdFiles { get; private set; } = [];
	public HostPrompt? Prompt { get; private set; }
	public PrinterNotification? Notification { get; private set; }
	public bool WaitingForUser { get; private set; }
	public int FeedRate { get; private set; } = 100;
	public int FlowRate { get; private set; } = 100;
	public int Layer => _layers?.Layer ?? 0;

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
		FeedRate = FlowRate = 100;
		Temperatures.Clear();
		ResetSdCard();
		ResetDialogs();
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
			// M20 rather than M21: re-initialising the card would break a print the printer runs from it.
			// M220/M221 without S report the current factors (a board that did not reset may still have changed ones).
			Enqueue(["M115", "M155 S2", "M220", "M221", "M20 L"]);

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
		ResetSdCard();
		ResetDialogs();
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

	public Result StartJob(string fileName, Stream content, long size, TimeSpan? estimate, PrintOptions? options = null)
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
			_screen = options?.ShowProgressOnPrinter == true ? new PrinterScreenReporter(fileName, options.LayerCount) : null;
			_layers = new LayerCounter();
			_scripts = options?.Scripts ?? PrintScripts.None;
			// Queued commands go out before the file's lines.
			QueueScript(_scripts.BeforePrint);
			State = PrinterState.Printing;
		}
		ShowOnScreen(_screen?.Start());
		Log(TerminalDirection.Info, $"Printing {fileName}");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		_work.Release();
		return Result.Success();
	}

	public Result PauseJob() => Pause(tellPrinter: true);

	/// <param name="tellPrinter">False when the printer paused itself and only reports it.</param>
	private Result Pause(bool tellPrinter)
	{
		JobKind kind;
		lock (_gate)
		{
			if (CurrentJob?.Status != JobStatus.Printing) return Result.Failure("Nothing is printing.");
			if (CurrentJob.Kind == JobKind.SdUpload) return Result.Failure("An SD card upload cannot be paused.");
			CurrentJob.Pause(Now);
			kind = CurrentJob.Kind;
			State = PrinterState.Paused;
		}
		Log(TerminalDirection.Info, "Print paused");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		// A printer that paused itself (M600, LCD) shows its own message.
		if (tellPrinter && kind == JobKind.Print) ShowOnScreen(_screen?.Paused());
		if (!tellPrinter) return Result.Success();
		return kind == JobKind.SdPrint ? Enqueue(["M25", .. _scripts.Pause]) : Enqueue(_scripts.Pause);
	}

	public Result ResumeJob() => Resume(tellPrinter: true);

	private Result Resume(bool tellPrinter)
	{
		lock (_gate)
		{
			if (CurrentJob?.Status != JobStatus.Paused) return Result.Failure("Print is not paused.");
			// Queued before the job streams again, so the resume script runs first.
			if (tellPrinter && CurrentJob.Kind == JobKind.Print) QueueScript(_scripts.Resume);
			CurrentJob.Resume(Now);
			State = PrinterState.Printing;
		}
		Log(TerminalDirection.Info, "Print resumed");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		if (tellPrinter && CurrentJob.Kind == JobKind.Print) ShowOnScreen(_screen?.Resumed());
		if (CurrentJob.Kind == JobKind.SdPrint) return tellPrinter ? Enqueue([.. _scripts.Resume, "M24"]) : Result.Success();
		_work.Release();
		return Result.Success();
	}

	public Result CancelJob(IEnumerable<string> cancelCommands)
	{
		IEnumerable<string> commands;
		string message;
		lock (_gate)
		{
			if (CurrentJob is not { IsActive: true }) return Result.Failure("No active print.");
			CurrentJob.Cancel(Now);
			_job?.Dispose();
			_job = null;
			State = PrinterState.Operational;
			IReadOnlyList<string> farewell = _screen?.Finish(JobStatus.Cancelled) ?? [];
			(commands, message) = CurrentJob.Kind switch
			{
				// Abort the SD print, then run the usual cancel script.
				JobKind.SdPrint => ([SdAbortCommand, "M27 S0", .. cancelCommands], "Print cancelled"),
				// Close the half-written file and delete it; nothing was printing, so no cancel script.
				JobKind.SdUpload => (_uploadOpened ? ["M29", $"M30 {_uploadTarget}", "M20 L"] : [], "SD card upload cancelled"),
				_ => (cancelCommands.Concat(farewell), "Print cancelled")
			};
			_screen = null;
			_scripts = PrintScripts.None;
		}
		Log(TerminalDirection.Info, message);
		events.Publish(PrinterChange.State | PrinterChange.Job);
		return Enqueue(commands);
	}

	/// <summary>Marlin aborts an SD print with M524; Prusa firmware has no M524 and stops a print with M603.</summary>
	private string SdAbortCommand =>
		Firmware?.StartsWith("Prusa", StringComparison.OrdinalIgnoreCase) == true ? "M603" : "M524";

	// ---------------------------------------------------------------- firmware dialogs

	public Result AnswerPrompt(int choice)
	{
		HostPrompt? prompt = Prompt;
		if (prompt is null) return Result.Failure("The printer is not asking anything.");
		if (choice < 0 || choice >= prompt.Choices.Count) return Result.Failure("Not one of the choices.");

		Prompt = null;
		Log(TerminalDirection.Info, $"Answered \"{prompt.Message}\": {prompt.Choices[choice]}");
		events.Publish(PrinterChange.Dialog);
		return SendOutOfBand($"M876 S{choice}");
	}

	public Result ContinueFromUser()
	{
		if (!WaitingForUser) return Result.Failure("The printer is not waiting.");
		return SendOutOfBand("M108");
	}

	public void DismissNotification()
	{
		Notification = null;
		events.Publish(PrinterChange.Dialog);
	}

	/// <summary>
	/// Writes an emergency-parser command straight away, past the one-command-in-flight queue:
	/// the printer is blocked on M600/M0 and would never get to it otherwise.
	/// </summary>
	private Result SendOutOfBand(string command)
	{
		IPrinterTransport? transport = _transport;
		if (transport is null || !State.IsConnected()) return Result.Failure("Printer is not connected.");

		lock (_gate)
		{
			bool inFlight = !_ok.Task.IsCompleted;
			_okToSkipAfter = inFlight ? 1 : 0;
			_okSkipDeadlineTicks = Now.UtcTicks + _outOfBandOkWindow.Ticks;
		}
		_ = Task.Run(async () =>
		{
			try
			{
				await transport.WriteLineAsync(command, CancellationToken.None);
				Log(TerminalDirection.Sent, command);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Out-of-band {Command} failed", command);
			}
		});
		return Result.Success();
	}

	/// <summary>True when this "ok" answers an out-of-band line and must not release the command in flight.</summary>
	private bool TakeOutOfBandOk()
	{
		lock (_gate)
		{
			if (_okToSkipAfter < 0) return false;
			if (Now.UtcTicks > _okSkipDeadlineTicks)
			{
				_okToSkipAfter = -1;
				return false;
			}
			if (_okToSkipAfter > 0)
			{
				_okToSkipAfter--;
				return false;
			}
			_okToSkipAfter = -1;
			return true;
		}
	}

	private void HandleAction(ActionCommand action)
	{
		switch (action.Name)
		{
			case "pause":
				if (Pause(tellPrinter: true).IsSuccess) Notify("Paused from the printer");
				break;
			case "paused":
				if (Pause(tellPrinter: false).IsSuccess) Notify("The printer paused the print");
				break;
			case "resume":
				Resume(tellPrinter: true);
				break;
			case "resumed":
				Resume(tellPrinter: false);
				break;
			case "cancel":
				if (CurrentJob is { IsActive: true }) _ = Task.Run(CancelFromPrinterAsync);
				break;
			case "disconnect":
				_ = Task.Run(DisconnectAsync);
				break;
			case "notification":
				if (action.Argument.Length > 0) Notify(action.Argument);
				break;
			case "out_of_filament":
				Notify(action.Argument.Length > 0 ? $"Filament runout ({action.Argument})" : "Filament runout");
				break;
			case "prompt_begin":
				_promptMessage = action.Argument;
				_promptChoices = [];
				break;
			case "prompt_choice" or "prompt_button":
				if (_promptMessage is not null && action.Argument.Length > 0) _promptChoices.Add(action.Argument);
				break;
			case "prompt_show":
				if (_promptMessage is null) break;
				// A prompt without buttons still needs an answer; Marlin's default is a single "Continue".
				Prompt = new HostPrompt(_promptMessage, _promptChoices.Count > 0 ? [.. _promptChoices] : ["Continue"]);
				events.Publish(PrinterChange.Dialog);
				break;
			case "prompt_end":
				_promptMessage = null;
				_promptChoices = [];
				if (Prompt is not null)
				{
					Prompt = null;
					events.Publish(PrinterChange.Dialog);
				}
				break;
		}
	}

	private async Task CancelFromPrinterAsync()
	{
		string[] script = [];
		if (settings is not null)
		{
			try { script = [.. PrintScripts.From(await settings.GetAsync(CancellationToken.None)).Cancel]; }
			catch (Exception ex) { logger.LogWarning(ex, "Could not read the cancel script"); }
		}
		if (CancelJob(script).IsSuccess) Notify("Cancelled from the printer");
	}

	private void Notify(string text)
	{
		Notification = new PrinterNotification(Now, text);
		Log(TerminalDirection.Info, text);
		events.Publish(PrinterChange.Dialog);
	}

	private void SetWaitingForUser(bool waiting)
	{
		if (WaitingForUser == waiting) return;
		WaitingForUser = waiting;
		events.Publish(PrinterChange.Dialog);
	}

	private void ResetDialogs()
	{
		Prompt = null;
		Notification = null;
		WaitingForUser = false;
		_promptMessage = null;
		_promptChoices = [];
		lock (_gate) _okToSkipAfter = -1;
		events.Publish(PrinterChange.Dialog);
	}

	private void ShowOnScreen(IReadOnlyList<string>? commands)
	{
		if (commands is { Count: > 0 }) Enqueue(commands);
	}

	/// <summary>Queues a script's lines without the connected check (the caller holds <c>_gate</c> and knows it is printing).</summary>
	private void QueueScript(IReadOnlyList<string> script)
	{
		foreach (string raw in script)
			if (GcodeLine.Clean(raw) is { } line) _manual.Enqueue(line);
	}

	// ---------------------------------------------------------------- SD card

	public Result RefreshSdFiles() => Enqueue(["M20 L"]);

	public Result InitSdCard()
	{
		if (CurrentJob is { Kind: not JobKind.Print, IsActive: true })
			return Result.Failure("The SD card is in use.");
		return Enqueue(["M21"]);
	}

	public Result ReleaseSdCard()
	{
		if (!State.IsConnected()) return Result.Failure("Printer is not connected.");
		if (CurrentJob is { Kind: not JobKind.Print, IsActive: true })
			return Result.Failure("The SD card is in use.");

		Result sent = Enqueue(["M22"]);
		SetSdCard(SdCardState.Unavailable, []);
		Log(TerminalDirection.Info, "SD card released");
		return sent;
	}

	public Result DeleteSdFile(string path)
	{
		if (!IsValidSdPath(path)) return Result.Failure("Not a file on the SD card.");
		if (CurrentJob is { Kind: JobKind.SdPrint, IsActive: true } j && SdFiles.Any(f => f.Path == path && f.DisplayName == j.FileName))
			return Result.Failure("This file is printing.");
		if (CurrentJob is { Kind: JobKind.SdUpload, IsActive: true })
			return Result.Failure("Wait for the SD card upload to finish.");
		return Enqueue([$"M30 {path}", "M20 L"]);
	}

	public Result StartSdPrint(string path, PrintScripts? scripts = null)
	{
		SdFile? file = SdFiles.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
		lock (_gate)
		{
			if (State != PrinterState.Operational)
				return Result.Failure(State.IsBusy() ? "A print is already running." : "Printer is not ready.");
			if (SdCard != SdCardState.Ready) return Result.Failure("No SD card.");
			if (file is null) return Result.Failure($"{path} is not on the SD card.");
			if (file.SizeBytes <= 0) return Result.Failure("The printer did not report the file's size.");

			CurrentJob = new PrintJob(file.DisplayName, file.SizeBytes, null, Now, JobKind.SdPrint);
			_layers = null;
			_scripts = scripts ?? PrintScripts.None;
			State = PrinterState.Printing;
		}
		Interlocked.Exchange(ref _lastSdProgressTicks, Now.UtcTicks);
		Log(TerminalDirection.Info, $"Printing {file.DisplayName} from the SD card");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		// M27 S2: report progress every 2 s; firmware without it gets polled with M27.
		return Enqueue([.. _scripts.BeforePrint, $"M23 {file.Path}", "M24", "M27 S2"]);
	}

	public Result<string> StartSdUpload(string fileName, Stream content, long size)
	{
		string target;
		lock (_gate)
		{
			string? refusal = State != PrinterState.Operational
				? State.IsBusy() ? "A print is already running." : "Printer is not ready."
				: SdCard != SdCardState.Ready ? "No SD card." : size <= 0 ? "File is empty." : null;
			if (refusal is not null)
			{
				content.Dispose();
				return Result.Failure<string>(refusal);
			}

			target = SdShortName.For(fileName, SdFiles.Select(f => f.Path));
			_job = new StreamReader(content, Encoding.UTF8);
			_uploadTarget = target;
			_uploadOpened = false;
			_uploadAccepted = false;
			CurrentJob = new PrintJob(fileName, size, null, Now, JobKind.SdUpload);
			_layers = null;
			State = PrinterState.Transferring;
		}
		Log(TerminalDirection.Info, $"Copying {fileName} to the SD card as {target}");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		_work.Release();
		return Result.Success(target);
	}

	private static bool IsValidSdPath(string path) =>
		!string.IsNullOrWhiteSpace(path) && !path.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is ';' or '*');

	private void ResetSdCard()
	{
		_sdListing = null;
		SetSdCard(SdCardState.Unknown, []);
	}

	private void SetSdCard(SdCardState state, IReadOnlyList<SdFile> files)
	{
		SdCard = state;
		SdFiles = files;
		events.Publish(PrinterChange.Files);
	}

	/// <summary>SD card replies: file list, progress, end of print, upload acknowledgement.</summary>
	private void HandleSdLine(string line)
	{
		if (_sdListing is { } listing)
		{
			if (line.StartsWith(SdCardReportParser.ListEnd, StringComparison.OrdinalIgnoreCase))
			{
				_sdListing = null;
				SetSdCard(SdCardState.Ready, listing
					.Select(f => f.LongName is null || f.LongName.Equals(f.Path, StringComparison.OrdinalIgnoreCase)
						? f with { LongName = _uploadedNames.GetValueOrDefault(f.Path, f.LongName!) }
						: f)
					.OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)
					.ToList());
			}
			else if (SdCardReportParser.TryParseFileEntry(line) is { } file) listing.Add(file);
			return;
		}

		if (line.StartsWith(SdCardReportParser.ListBegin, StringComparison.OrdinalIgnoreCase))
			_sdListing = [];
		else if (SdCardReportParser.IsCardReady(line))
		{
			SetSdCard(SdCardState.Ready, SdFiles);
			Enqueue(["M20 L"]);
		}
		else if (SdCardReportParser.IsCardGone(line))
		{
			SetSdCard(SdCardState.Unavailable, []);
			EndSdPrint(JobStatus.Failed, "SD card removed during the print");
		}
		else if (SdCardReportParser.TryParseProgress(line) is { } progress)
		{
			Interlocked.Exchange(ref _lastSdProgressTicks, Now.UtcTicks);
			bool moved;
			lock (_gate)
			{
				moved = CurrentJob is { Kind: JobKind.SdPrint, IsActive: true };
				if (moved) CurrentJob!.ReportPosition(progress.Done);
			}
			if (moved) events.Publish(PrinterChange.Job);
		}
		else if (line.Contains(SdCardReportParser.PrintDone, StringComparison.OrdinalIgnoreCase))
			EndSdPrint(JobStatus.Completed, null);
		else if (line.Contains("Writing to file", StringComparison.OrdinalIgnoreCase))
			_uploadAccepted = true;
		else if (line.Contains("open failed", StringComparison.OrdinalIgnoreCase))
			EndSdPrint(JobStatus.Failed, "The printer could not open the file on the SD card");
	}

	private void EndSdPrint(JobStatus status, string? error)
	{
		string? name;
		PrintScripts scripts;
		lock (_gate)
		{
			if (CurrentJob is not { Kind: JobKind.SdPrint, IsActive: true }) return;
			if (status == JobStatus.Completed) CurrentJob.Complete(Now);
			else CurrentJob.Fail(Now);
			name = CurrentJob.FileName;
			State = PrinterState.Operational;
			scripts = _scripts;
			_scripts = PrintScripts.None;
		}
		if (error is null) Log(TerminalDirection.Info, $"Print finished: {name}");
		else Log(TerminalDirection.Error, $"{error}: {name}");
		events.Publish(PrinterChange.State | PrinterChange.Job);
		Enqueue(status == JobStatus.Completed ? ["M27 S0", .. scripts.AfterPrint] : ["M27 S0"]);
	}

	private string? NextCommand()
	{
		bool uploading;
		lock (_gate) uploading = CurrentJob is { Kind: JobKind.SdUpload, IsActive: true };

		// While the printer writes to the SD card every line lands in the file, so other commands wait.
		if (!uploading && _manual.TryDequeue(out string? manual)) return manual;

		bool refused;
		string name;
		PrintScripts finished;
		lock (_gate)
		{
			if (CurrentJob?.Status != JobStatus.Printing || _job is null) return null;
			PrintJob job = CurrentJob;
			name = job.FileName;
			StreamReader reader = _job;

			if (uploading && !_uploadOpened)
			{
				_uploadOpened = true;
				return $"M28 {_uploadTarget}";
			}

			// Without "Writing to file" the printer would run the G-code instead of storing it.
			refused = uploading && !_uploadAccepted;
			while (!refused && reader.ReadLine() is { } raw)
			{
				job.Advance(Encoding.UTF8.GetByteCount(raw) + 1);
				if (_layers?.OnLine(raw) is not null) Interlocked.Exchange(ref _layerChanged, 1);
				if (_screen is { } screen)
				{
					if (screen.OnProgress(job.Progress, job.EstimatedRemaining(Now), Now) is { } progress) _manual.Enqueue(progress);
					// Layer markers are comments: the message goes out in their place, before the layer's first move.
					if (screen.OnLine(raw) is { } message) return message;
				}
				if (GcodeLine.Clean(raw) is not { } line) continue;
				// A stray M28/M29 in the file would end the upload early.
				if (uploading && GcodeLine.CommandOf(line) is "M28" or "M29") continue;
				return line;
			}

			if (refused) job.Fail(Now);
			else job.Complete(Now);
			reader.Dispose();
			_job = null;
			State = PrinterState.Operational;
			finished = _scripts;
			_scripts = PrintScripts.None;
		}

		if (refused)
		{
			Log(TerminalDirection.Error, $"The printer did not accept {_uploadTarget} for writing");
			events.Publish(PrinterChange.State | PrinterChange.Job);
			return null;
		}

		events.Publish(PrinterChange.State | PrinterChange.Job);
		if (!uploading)
		{
			Log(TerminalDirection.Info, $"Print finished: {name}");
			ShowOnScreen(_screen?.Finish(JobStatus.Completed));
			_screen = null;
			Enqueue(finished.AfterPrint);
			return null;
		}

		Log(TerminalDirection.Info, $"Copied {name} to the SD card as {_uploadTarget}");
		_uploadedNames[_uploadTarget!] = name;
		_manual.Enqueue("M20 L");
		return "M29";
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
				if (Interlocked.Exchange(ref _layerChanged, 0) == 1) events.Publish(PrinterChange.Layer);
				if (command is null)
				{
					await _work.WaitAsync(TimeSpan.FromMilliseconds(500), ct);
					continue;
				}

				TrackRates(command);
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

	/// <summary>Follows M220/M221 from any source: the sliders, the terminal or the file being printed.</summary>
	private void TrackRates(string command)
	{
		if (RateReportParser.FeedSetBy(command) is { } feed) SetRates(feed, FlowRate);
		else if (RateReportParser.FlowSetBy(command) is { } flow) SetRates(FeedRate, flow);
	}

	private void SetRates(int feed, int flow)
	{
		if (feed == FeedRate && flow == FlowRate) return;
		(FeedRate, FlowRate) = (feed, flow);
		events.Publish(PrinterChange.State);
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

		if (RateReportParser.TryParseFeedReport(line) is { } feed) SetRates(feed, FlowRate);
		else if (RateReportParser.TryParseFlowReport(line) is { } flow) SetRates(FeedRate, flow);

		HandleSdLine(line);

		if (ActionCommand.TryParse(line) is { } action)
		{
			HandleAction(action);
			return;
		}

		// Marlin's host keepalive while M0/M1/M600 wait for the user.
		if (line.Contains("busy: paused for", StringComparison.OrdinalIgnoreCase))
			SetWaitingForUser(true);
		else if (line.StartsWith("Cap:PROMPT_SUPPORT:1", StringComparison.Ordinal))
			Enqueue(["M876 P1"]); // tell the firmware this host shows its prompts

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
		{
			SetWaitingForUser(false);
			if (!TakeOutOfBandOk()) _ok.TrySetResult();
		}
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

				// Firmware without M27 auto-report gets asked for SD progress.
				if (CurrentJob is { Kind: JobKind.SdPrint, Status: JobStatus.Printing }
					&& Now.UtcTicks - Interlocked.Read(ref _lastSdProgressTicks) > _sdProgressInterval.Ticks)
				{
					Interlocked.Exchange(ref _lastSdProgressTicks, Now.UtcTicks);
					Enqueue(["M27"]);
				}
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
		TerminalEntry entry = new(Interlocked.Increment(ref _terminalId), Now, direction, text);
		_terminal.Add(entry);
		serialLog?.Write(entry);
		events.Publish(PrinterChange.Terminal);
	}

	/// <summary>
	/// Safe to call more than once: the host disposes the session once for each service it is registered as
	/// (<see cref="PrinterSession"/> and <see cref="IPrinterSession"/>).
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		await TeardownAsync();
		Interlocked.Exchange(ref _cts, null)?.Dispose();
	}
}
