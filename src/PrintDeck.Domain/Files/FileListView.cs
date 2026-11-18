using PrintDeck.Domain.Files.Enums;

namespace PrintDeck.Domain.Files;

/// <summary>Search, sort and filter rules for the file list.</summary>
public static class FileListView
{
	/// <param name="search">Every word must appear in the path, ignoring case ("benchy pla" finds "Benchy_0.2mm_PLA.gcode").</param>
	/// <param name="hidePrinted">Leaves out files that have printed successfully at least once.</param>
	/// <param name="folder">
	/// Null for every file. Otherwise the files directly in that folder ("" is the top level), or, while searching,
	/// every match anywhere inside it.
	/// </param>
	public static IReadOnlyList<GcodeFile> Apply(IEnumerable<GcodeFile> files, string? search, FileSort sort, bool hidePrinted, string? folder = null)
	{
		string[] words = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		IEnumerable<GcodeFile> shown = files
			.Where(f => folder is null || (words.Length > 0 ? FilePath.IsWithin(FilePath.Folder(f.Name), folder) : FilePath.Folder(f.Name) == folder))
			.Where(f => words.All(w => f.Name.Contains(w, StringComparison.OrdinalIgnoreCase)))
			.Where(f => !hidePrinted || (f.Prints?.Completed ?? 0) == 0);

		return (sort switch
		{
			FileSort.Name => shown.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
			FileSort.LastPrinted => shown
				.OrderByDescending(f => f.Prints?.Last?.EndedAt ?? DateTimeOffset.MinValue)
				.ThenByDescending(f => f.UploadedAt),
			FileSort.Size => shown.OrderByDescending(f => f.SizeBytes).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
			_ => shown.OrderByDescending(f => f.UploadedAt).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
		}).ToList();
	}
}
