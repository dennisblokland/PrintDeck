using System.Text;

using Microsoft.Extensions.Options;

using PrintDeck.Domain.Files;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Infrastructure.Storage;
using PrintDeck.Infrastructure.Tests.TestSupport;

namespace PrintDeck.Infrastructure.Tests.Storage;

public sealed class FileSystemGcodeStoreTests : IDisposable
{
	private readonly TempDirectory _dir = new();
	private readonly FileSystemGcodeStore _store;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public FileSystemGcodeStoreTests()
	{
		_store = new FileSystemGcodeStore(Options.Create(new StorageOptions { DataDirectory = _dir.Path }));
	}

	public void Dispose() => _dir.Dispose();

	private string Uploads => Path.Combine(_dir.Path, "uploads");

	private Task<GcodeFile> Save(string name, string content = "G28\n") =>
		_store.SaveAsync(name, new MemoryStream(Encoding.UTF8.GetBytes(content)), Ct);

	[Fact]
	public async Task Saves_into_the_uploads_directory()
	{
		GcodeFile file = await Save("cube.gcode", "G28\nG1 X10\n");

		Assert.Equal("cube.gcode", file.Name);
		Assert.Equal(11, file.SizeBytes);
		Assert.Null(file.Analysis);
		Assert.Equal("G28\nG1 X10\n", await File.ReadAllTextAsync(Path.Combine(Uploads, "cube.gcode"), Ct));
		Assert.False(File.Exists(Path.Combine(Uploads, "cube.gcode.part")));
	}

	[Fact]
	public async Task Open_read_returns_the_content()
	{
		await Save("cube.gcode", "G28\n");
		using StreamReader reader = new(_store.OpenRead("cube.gcode"));
		Assert.Equal("G28\n", await reader.ReadToEndAsync(Ct));
	}

	[Fact]
	public async Task Keeps_the_analysis_in_a_sidecar_file()
	{
		GcodeAnalysis analysis = new(TimeSpan.FromMinutes(90), 1234.5, 42, 1000, "OrcaSlicer");
		await Save("cube.gcode");
		await _store.SaveAnalysisAsync("cube.gcode", analysis, Ct);

		Assert.Equal(analysis, (await _store.GetAsync("cube.gcode", Ct))!.Analysis);
		Assert.True(File.Exists(Path.Combine(Uploads, "cube.gcode.meta.json")));
	}

	[Fact]
	public async Task Re_uploading_drops_the_stale_analysis()
	{
		await Save("cube.gcode");
		await _store.SaveAnalysisAsync("cube.gcode", new GcodeAnalysis(null, null, null, 1, null), Ct);

		await Save("cube.gcode", "G28\nG28\n");

		GcodeFile file = (await _store.GetAsync("cube.gcode", Ct))!;
		Assert.Null(file.Analysis);
		Assert.Equal(8, file.SizeBytes);
	}

	[Fact]
	public async Task Ignores_a_corrupt_sidecar()
	{
		await Save("cube.gcode");
		await File.WriteAllTextAsync(Path.Combine(Uploads, "cube.gcode.meta.json"), "{ not json", Ct);

		Assert.Null((await _store.GetAsync("cube.gcode", Ct))!.Analysis);
	}

	[Fact]
	public async Task Lists_files_without_sidecars()
	{
		await Save("a.gcode");
		await Save("b.gcode");
		await _store.SaveAnalysisAsync("a.gcode", new GcodeAnalysis(null, null, null, 1, null), Ct);

		IReadOnlyList<GcodeFile> files = await _store.ListAsync(Ct);

		Assert.Equal(["a.gcode", "b.gcode"], files.Select(f => f.Name).Order());
	}

	[Fact]
	public async Task Get_returns_null_for_a_missing_file()
	{
		Assert.Null(await _store.GetAsync("nope.gcode", Ct));
	}

	[Fact]
	public async Task Delete_removes_file_and_sidecar()
	{
		await Save("cube.gcode");
		await _store.SaveAnalysisAsync("cube.gcode", new GcodeAnalysis(null, null, null, 1, null), Ct);

		Assert.True(await _store.DeleteAsync("cube.gcode", Ct));
		Assert.Empty(Directory.EnumerateFileSystemEntries(Uploads));
	}

	[Fact]
	public async Task Delete_reports_a_missing_file()
	{
		Assert.False(await _store.DeleteAsync("nope.gcode", Ct));
	}

	[Theory]
	[InlineData("../settings.json")]
	[InlineData("../../escape.gcode")]
	[InlineData("/etc/passwd")]
	[InlineData("")]
	public async Task Refuses_paths_outside_the_uploads_directory(string name)
	{
		await Assert.ThrowsAsync<ArgumentException>(() => _store.GetAsync(name, Ct));
		await Assert.ThrowsAsync<ArgumentException>(() => Save(name));
		Assert.Throws<ArgumentException>(() => _store.OpenRead(name));
	}

	[Fact]
	public async Task Records_prints_next_to_the_file_and_counts_them()
	{
		DateTimeOffset t0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
		await Save("cube.gcode");
		Assert.Equal(PrintStats.None, (await _store.GetAsync("cube.gcode", Ct))!.Prints);

		await _store.RecordPrintAsync("cube.gcode", new PrintRecord(t0, t0.AddHours(1), JobStatus.Completed), Ct);
		await _store.RecordPrintAsync("cube.gcode", new PrintRecord(t0.AddHours(2), t0.AddHours(2.5), JobStatus.Cancelled), Ct);

		GcodeFile file = Assert.Single(await _store.ListAsync(Ct)); // the .prints.json sidecar is not a file of its own
		Assert.Equal((1, 1), (file.Prints!.Completed, file.Prints.Unsuccessful));
		Assert.Equal(JobStatus.Cancelled, file.Prints.Last!.Status);
		Assert.Contains("\"Completed\"", await File.ReadAllTextAsync(Path.Combine(Uploads, "cube.gcode.prints.json"), Ct));
	}

	[Fact]
	public async Task History_survives_a_new_upload_and_goes_with_delete()
	{
		DateTimeOffset t0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
		await Save("cube.gcode");
		await _store.RecordPrintAsync("cube.gcode", new PrintRecord(t0, t0.AddHours(1), JobStatus.Completed), Ct);

		await Save("cube.gcode", "G28\nG1 X5\n");
		Assert.Equal(1, (await _store.GetAsync("cube.gcode", Ct))!.Prints!.Completed);

		await _store.DeleteAsync("cube.gcode", Ct);
		Assert.False(File.Exists(Path.Combine(Uploads, "cube.gcode.prints.json")));
	}

	[Fact]
	public async Task A_print_of_a_deleted_file_is_dropped()
	{
		await _store.RecordPrintAsync("gone.gcode", new PrintRecord(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, JobStatus.Completed), Ct);
		Assert.Empty(Directory.EnumerateFiles(Uploads));
	}

	[Fact]
	public async Task Lists_the_prints_of_every_file_and_skips_orphaned_history()
	{
		DateTimeOffset t0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
		await Save("cube.gcode");
		await Save("benchy.gcode");
		await _store.RecordPrintAsync("cube.gcode", new PrintRecord(t0, t0.AddHours(1), JobStatus.Completed, TimeSpan.FromHours(1), 1234.5), Ct);
		await _store.RecordPrintAsync("benchy.gcode", new PrintRecord(t0, t0.AddHours(2), JobStatus.Failed), Ct);
		await File.WriteAllTextAsync(Path.Combine(Uploads, "gone.gcode.prints.json"), "[]", Ct);

		IReadOnlyList<PrintHistoryEntry> prints = await _store.ListPrintsAsync(Ct);

		Assert.Equal(["benchy.gcode", "cube.gcode"], prints.Select(p => p.FileName).Order());
		PrintRecord cube = prints.Single(p => p.FileName == "cube.gcode").Print;
		Assert.Equal((TimeSpan.FromHours(1), (double?) 1234.5), (cube.PrintTime, cube.FilamentMm));
	}

	[Fact]
	public async Task Reads_history_written_before_time_and_filament_were_kept()
	{
		await Save("cube.gcode");
		await File.WriteAllTextAsync(Path.Combine(Uploads, "cube.gcode.prints.json"),
			"""[{"startedAt":"2026-11-16T19:14:28+00:00","endedAt":"2026-11-16T19:14:30+00:00","status":"Completed","succeeded":true}]""", Ct);

		PrintRecord print = Assert.Single(await _store.ListPrintsAsync(Ct)).Print;

		Assert.Equal((JobStatus.Completed, TimeSpan.Zero, (double?) null), (print.Status, print.PrintTime, print.FilamentMm));
	}
}
