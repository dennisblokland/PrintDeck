using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.SdCard;
using PrintDeck.Domain.SdCard.Enums;
using PrintDeck.Domain.Temperatures;
using PrintDeck.Domain.Terminal;

namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>Records what handlers ask of the session; state is set by the test.</summary>
internal sealed class FakePrinterSession : IPrinterSession
{
	public PrinterState State { get; set; } = PrinterState.Operational;
	public string? Port { get; set; }
	public int BaudRate { get; set; }
	public string? Firmware { get; set; }
	public string? ErrorMessage { get; set; }
	public PrintJob? CurrentJob { get; set; }
	public TemperatureHistory Temperatures { get; } = new();
	public SdCardState SdCard { get; set; } = SdCardState.Ready;
	public IReadOnlyList<SdFile> SdFiles { get; set; } = [];
	public List<string> SdCalls { get; } = [];
	public (string FileName, long Size)? SdUpload { get; private set; }
	public List<TerminalEntry> TerminalEntries { get; } = [];

	public List<string> Enqueued { get; } = [];
	public List<string> CancelCommands { get; } = [];
	public (string FileName, long Size, TimeSpan? Estimate)? StartedJob { get; private set; }
	public (string Port, int BaudRate)? ConnectedWith { get; private set; }
	public bool Disconnected { get; private set; }
	public bool TerminalCleared { get; private set; }

	public Result ConnectResult { get; set; } = Result.Success();
	public Result JobResult { get; set; } = Result.Success();

	public IReadOnlyList<TerminalEntry> Terminal(int last) => TerminalEntries.TakeLast(last).ToList();
	public void ClearTerminal() => TerminalCleared = true;

	public Task<Result> ConnectAsync(string port, int baudRate, CancellationToken ct)
	{
		ConnectedWith = (port, baudRate);
		return Task.FromResult(ConnectResult);
	}

	public Task DisconnectAsync()
	{
		Disconnected = true;
		return Task.CompletedTask;
	}

	public Result Enqueue(IEnumerable<string> commands)
	{
		Enqueued.AddRange(commands);
		return Result.Success();
	}

	public Result StartJob(string fileName, Stream content, long size, TimeSpan? estimate)
	{
		content.Dispose();
		StartedJob = (fileName, size, estimate);
		return JobResult;
	}

	public Result PauseJob() => JobResult;
	public Result ResumeJob() => JobResult;

	public Result CancelJob(IEnumerable<string> cancelCommands)
	{
		CancelCommands.AddRange(cancelCommands);
		return JobResult;
	}

	public Result RefreshSdFiles() => Sd("refresh");
	public Result InitSdCard() => Sd("init");
	public Result ReleaseSdCard() => Sd("release");
	public Result DeleteSdFile(string path) => Sd("delete " + path);
	public Result StartSdPrint(string path) => Sd("print " + path);

	public Result<string> StartSdUpload(string fileName, Stream content, long size)
	{
		content.Dispose();
		SdUpload = (fileName, size);
		return JobResult.IsSuccess ? Result.Success("FILE.GCO") : Result.Failure<string>(JobResult.Error!);
	}

	private Result Sd(string call)
	{
		SdCalls.Add(call);
		return JobResult;
	}

	/// <summary>Makes the latest temperature report contain the given heaters.</summary>
	public void ReportTemperatures(params HeaterReading[] heaters) =>
		Temperatures.Record(new TemperatureSample(DateTimeOffset.UtcNow, heaters));
}
