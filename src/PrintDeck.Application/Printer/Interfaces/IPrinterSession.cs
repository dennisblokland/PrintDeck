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

	/// <summary>Question the firmware is asking (<c>//action:prompt_*</c>), if any.</summary>
	HostPrompt? Prompt { get; }

	/// <summary>Latest message from the printer, until dismissed.</summary>
	PrinterNotification? Notification { get; }

	/// <summary>The printer reports "busy: paused for user" (M0/M1/M600 waiting).</summary>
	bool WaitingForUser { get; }

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

	/// <summary>Answers <see cref="Prompt"/> with <c>M876 S&lt;choice&gt;</c>, sent past the command queue.</summary>
	Result AnswerPrompt(int choice);

	/// <summary>Releases an M0/M1/M600 wait with <c>M108</c>, sent past the command queue.</summary>
	Result ContinueFromUser();

	void DismissNotification();

	/// <summary>Copies a file onto the SD card; returns the 8.3 name it gets there.</summary>
	Result<string> StartSdUpload(string fileName, Stream content, long size);
}
