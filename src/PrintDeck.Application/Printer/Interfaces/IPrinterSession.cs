using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
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

	IReadOnlyList<TerminalEntry> Terminal(int last);
	void ClearTerminal();

	Task<Result> ConnectAsync(string port, int baudRate, CancellationToken ct);
	Task DisconnectAsync();

	Result Enqueue(IEnumerable<string> commands);
	Result StartJob(string fileName, Stream content, long size, TimeSpan? estimate);
	Result PauseJob();
	Result ResumeJob();
	Result CancelJob(IEnumerable<string> cancelCommands);
}
