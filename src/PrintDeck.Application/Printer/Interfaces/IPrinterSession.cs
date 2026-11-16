using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.SdCard;
using PrintDeck.Domain.SdCard.Enums;
using PrintDeck.Domain.Temperatures;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Printer.Interfaces;

/// <summary>The one live connection to the printer. Singleton.</summary>
public interface IPrinterSession
{
	PrinterState State { get; }
	string? Port { get; }
	int BaudRate { get; }
	string? Firmware { get; }
	string? ErrorMessage { get; }
	PrintJob? CurrentJob { get; }
	TemperatureHistory Temperatures { get; }
	SdCardState SdCard { get; }
	IReadOnlyList<SdFile> SdFiles { get; }

	IReadOnlyList<TerminalEntry> Terminal(int last);
	void ClearTerminal();

	Task<Result> ConnectAsync(string port, int baudRate, CancellationToken ct);
	Task DisconnectAsync();

	Result Enqueue(IEnumerable<string> commands);
	Result StartJob(string fileName, Stream content, long size, TimeSpan? estimate);
	Result PauseJob();
	Result ResumeJob();
	Result CancelJob(IEnumerable<string> cancelCommands);

	/// <summary>Asks for the file list (<c>M20</c>); <see cref="SdFiles"/> updates when it arrives.</summary>
	Result RefreshSdFiles();
	Result InitSdCard();
	Result ReleaseSdCard();
	Result DeleteSdFile(string path);
	Result StartSdPrint(string path);

	/// <summary>Copies a file onto the SD card; returns the 8.3 name it gets there.</summary>
	Result<string> StartSdUpload(string fileName, Stream content, long size);
}
