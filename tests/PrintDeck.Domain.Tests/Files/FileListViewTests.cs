using PrintDeck.Domain.Files;
using PrintDeck.Domain.Files.Enums;
using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Domain.Tests.Files;

public class FileListViewTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

	private static PrintStats Printed(int daysAgo, JobStatus status) =>
		PrintStats.From([new PrintRecord(_t0.AddDays(-daysAgo).AddHours(-1), _t0.AddDays(-daysAgo), status)]);

	private static readonly GcodeFile[] _files =
	[
		new("Benchy_0.2mm_PLA.gcode", 3_000, _t0.AddDays(-10), null, Printed(1, JobStatus.Completed)),
		new("bracket.gcode", 9_000, _t0.AddDays(-2), null, Printed(5, JobStatus.Failed)),
		new("Cube.gcode", 500, _t0.AddDays(-5), null, PrintStats.None),
		new("vase PETG.gcode", 20_000, _t0, null, null)
	];

	private static string[] Names(string? search = null, FileSort sort = FileSort.Uploaded, bool hidePrinted = false) =>
		FileListView.Apply(_files, search, sort, hidePrinted).Select(f => f.Name).ToArray();

	private static readonly GcodeFile[] _nested =
	[
		new("top.gcode", 1, _t0, null),
		new("parts/bracket.gcode", 1, _t0, null),
		new("parts/brackets/bracket-left.gcode", 1, _t0, null),
		new("vases/bracket vase.gcode", 1, _t0, null)
	];

	private static string[] In(string? folder, string? search = null) =>
		FileListView.Apply(_nested, search, FileSort.Name, false, folder).Select(f => f.Name).ToArray();

	[Fact]
	public void Without_a_folder_lists_every_file() => Assert.Equal(4, In(null).Length);

	[Fact]
	public void A_folder_lists_only_its_own_files()
	{
		Assert.Equal(["top.gcode"], In(""));
		Assert.Equal(["parts/bracket.gcode"], In("parts"));
		Assert.Equal(["parts/brackets/bracket-left.gcode"], In("parts/brackets"));
	}

	[Fact]
	public void Searching_a_folder_looks_in_its_subfolders_too()
	{
		Assert.Equal(["parts/bracket.gcode", "parts/brackets/bracket-left.gcode", "vases/bracket vase.gcode"], In("", "bracket"));
		Assert.Equal(["parts/bracket.gcode", "parts/brackets/bracket-left.gcode"], In("parts", "bracket"));
		Assert.Equal(["vases/bracket vase.gcode"], In("", "vases bracket"));
	}

	[Fact]
	public void Defaults_to_newest_upload_first() =>
		Assert.Equal(["vase PETG.gcode", "bracket.gcode", "Cube.gcode", "Benchy_0.2mm_PLA.gcode"], Names());

	[Fact]
	public void Sorts_by_name_ignoring_case() =>
		Assert.Equal(["Benchy_0.2mm_PLA.gcode", "bracket.gcode", "Cube.gcode", "vase PETG.gcode"], Names(sort: FileSort.Name));

	[Fact]
	public void Sorts_by_last_print_with_never_printed_last() =>
		Assert.Equal(["Benchy_0.2mm_PLA.gcode", "bracket.gcode", "vase PETG.gcode", "Cube.gcode"], Names(sort: FileSort.LastPrinted));

	[Fact]
	public void Sorts_by_size_largest_first() =>
		Assert.Equal(["vase PETG.gcode", "bracket.gcode", "Benchy_0.2mm_PLA.gcode", "Cube.gcode"], Names(sort: FileSort.Size));

	[Theory]
	[InlineData("benchy", new[] { "Benchy_0.2mm_PLA.gcode" })]
	[InlineData("  PLA  benchy ", new[] { "Benchy_0.2mm_PLA.gcode" })]
	[InlineData("b", new[] { "bracket.gcode", "Cube.gcode", "Benchy_0.2mm_PLA.gcode" })]
	[InlineData("petg vase", new[] { "vase PETG.gcode" })]
	[InlineData("abs", new string[0])]
	[InlineData("", new[] { "vase PETG.gcode", "bracket.gcode", "Cube.gcode", "Benchy_0.2mm_PLA.gcode" })]
	public void Search_needs_every_word_in_the_name(string search, string[] expected) => Assert.Equal(expected, Names(search));

	[Fact]
	public void Hide_printed_leaves_out_only_successful_prints() =>
		Assert.Equal(["vase PETG.gcode", "bracket.gcode", "Cube.gcode"], Names(hidePrinted: true));

	[Fact]
	public void Stats_add_up_print_time_filament_and_success_rate()
	{
		PrintStats stats = PrintStats.From(
		[
			new PrintRecord(_t0, _t0.AddHours(2), JobStatus.Completed, TimeSpan.FromHours(1.5), 4200),
			new PrintRecord(_t0, _t0.AddHours(3), JobStatus.Completed, TimeSpan.FromHours(2), 5000.5),
			new PrintRecord(_t0, _t0.AddHours(4), JobStatus.Cancelled, TimeSpan.FromMinutes(30), null),
			new PrintRecord(_t0, _t0.AddHours(5), JobStatus.Failed) // older record: no time, no filament
		]);

		Assert.Equal(TimeSpan.FromHours(4), stats.TotalPrintTime);
		Assert.Equal(9200.5, stats.TotalFilamentMm);
		Assert.Equal(0.5, stats.SuccessRate);
		Assert.Null(PrintStats.None.SuccessRate);
	}

	[Fact]
	public void Stats_count_outcomes_and_keep_the_latest()
	{
		PrintStats stats = PrintStats.From(
		[
			new PrintRecord(_t0, _t0.AddHours(2), JobStatus.Completed),
			new PrintRecord(_t0.AddDays(1), _t0.AddDays(1).AddMinutes(5), JobStatus.Cancelled),
			new PrintRecord(_t0.AddHours(-5), _t0.AddHours(-4), JobStatus.Failed)
		]);

		Assert.Equal((1, 2, 3), (stats.Completed, stats.Unsuccessful, stats.Total));
		Assert.Equal(JobStatus.Cancelled, stats.Last!.Status);
		Assert.Same(PrintStats.None.Last, PrintStats.From([]).Last);
	}
}
