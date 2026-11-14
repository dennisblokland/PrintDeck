using PrintDeck.Application.Abstractions.Enums;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Line-oriented link to a printer (serial port, virtual printer, ...).</summary>
public interface IPrinterTransport : IAsyncDisposable
{
	Task OpenAsync(CancellationToken ct);
	Task WriteLineAsync(string line, CancellationToken ct);
	IAsyncEnumerable<string> ReadLinesAsync(CancellationToken ct);
}
