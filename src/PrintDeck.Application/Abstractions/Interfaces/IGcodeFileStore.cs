using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Stored uploads. Names are paths with '/' between folders (see <see cref="FilePath"/>), e.g. "parts/benchy.gcode".</summary>
public interface IGcodeFileStore
{
	/// <summary>Every file in every folder.</summary>
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

	/// <summary>Every folder, nested ones included ("parts", "parts/brackets"), sorted.</summary>
	IReadOnlyList<string> ListFolders();

	/// <summary>Creates the folder and any missing parents; false when it already existed.</summary>
	bool CreateFolder(string path);

	/// <summary>Moves (or renames) a file with its analysis, history and thumbnail; false when it doesn't exist. Creates the target folder.</summary>
	Task<bool> MoveAsync(string from, string to, CancellationToken ct);

	/// <summary>Moves (or renames) a folder with everything in it; false when it doesn't exist.</summary>
	bool MoveFolder(string from, string to);

	/// <summary>Removes an empty folder; false when it doesn't exist or isn't empty.</summary>
	bool DeleteFolder(string path);
}
