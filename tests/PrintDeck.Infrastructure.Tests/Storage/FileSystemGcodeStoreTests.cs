using System.Text;

using Microsoft.Extensions.Options;

using PrintDeck.Domain.Files;
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
}
