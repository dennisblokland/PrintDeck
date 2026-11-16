using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Abstractions.Interfaces;

public interface IGcodeFileStore
{
	Task<IReadOnlyList<GcodeFile>> ListAsync(CancellationToken ct);
	Task<GcodeFile?> GetAsync(string name, CancellationToken ct);
	Task<GcodeFile> SaveAsync(string name, Stream content, CancellationToken ct);
	Task SaveAnalysisAsync(string name, GcodeAnalysis analysis, CancellationToken ct);
	Stream OpenRead(string name);
	Task<bool> DeleteAsync(string name, CancellationToken ct);

	/// <summary>Adds a finished print to the file's history; ignored when the file is gone.</summary>
	Task RecordPrintAsync(string name, PrintRecord print, CancellationToken ct);
}
