using PrintDeck.Domain.Files.Enums;

namespace PrintDeck.Domain.Files;

/// <summary>Search, sort and filter rules for the file list.</summary>
public static class FileListView
{
	/// <param name="search">Every word must appear in the name, ignoring case ("benchy pla" finds "Benchy_0.2mm_PLA.gcode").</param>
	/// <param name="hidePrinted">Leaves out files that have printed successfully at least once.</param>
	public static IReadOnlyList<GcodeFile> Apply(IEnumerable<GcodeFile> files, string? search, FileSort sort, bool hidePrinted)
	{
		string[] words = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		IEnumerable<GcodeFile> shown = files
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
