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

	/// <summary>Stores the file's slicer thumbnail, replacing an earlier one; ignored when the file is gone.</summary>
	Task SaveThumbnailAsync(string name, GcodeThumbnail thumbnail, CancellationToken ct);

	/// <summary>The stored thumbnail and its content type; null when the file has none.</summary>
	(Stream Content, string ContentType)? OpenThumbnail(string name);

	/// <summary>Every recorded print of every stored file, in no particular order.</summary>
	Task<IReadOnlyList<PrintHistoryEntry>> ListPrintsAsync(CancellationToken ct);
}
