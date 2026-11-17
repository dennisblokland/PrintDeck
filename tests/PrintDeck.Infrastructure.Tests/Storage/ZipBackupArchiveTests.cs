using System.IO.Compression;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using PrintDeck.Domain.Backup;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Settings;
using PrintDeck.Infrastructure.Storage;
using PrintDeck.Infrastructure.Tests.TestSupport;

namespace PrintDeck.Infrastructure.Tests.Storage;

/// <summary>Backups round-trip between two data directories through the real file and settings stores.</summary>
public sealed class ZipBackupArchiveTests : IDisposable
{
	private static readonly DateTimeOffset _now = new(2026, 11, 17, 15, 30, 0, TimeSpan.Zero);
	private readonly TempDirectory _source = new();
	private readonly TempDirectory _target = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public void Dispose()
	{
		_source.Dispose();
		_target.Dispose();
	}

	private sealed record Host(FileSystemGcodeStore Files, JsonSettingsStore Settings, ZipBackupArchive Archive, string Uploads);

	private static Host HostIn(TempDirectory dir)
	{
		IOptions<StorageOptions> options = Options.Create(new StorageOptions { DataDirectory = dir.Path });
		JsonSettingsStore settings = new(options);
		return new Host(new FileSystemGcodeStore(options), settings, new ZipBackupArchive(options, settings, new FixedTime(_now)),
			options.Value.UploadsDirectory);
	}

	private sealed class FixedTime(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}

	private static GcodeAnalysis Analysis(int layers) => new(TimeSpan.FromMinutes(10), 1000, layers, 500, "PrusaSlicer");

	private static async Task Upload(Host host, string name, string content) =>
		await host.Files.SaveAsync(name, new MemoryStream(Encoding.UTF8.GetBytes(content)), Ct);

	private static async Task<byte[]> Backup(Host host)
	{
		WriteOnlyStream body = new();
		await host.Archive.WriteAsync(body, Ct);
		return body.ToArray();
	}

	private static Task<Result<BackupRestore>> Restore(Host host, byte[] zip) => host.Archive.RestoreAsync(new MemoryStream(zip), Ct);

	private static byte[] Zip(params (string Name, string Content)[] entries)
	{
		MemoryStream stream = new();
		using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
			foreach ((string name, string content) in entries)
			{
				using StreamWriter w = new(zip.CreateEntry(name).Open());
				w.Write(content);
			}
		return stream.ToArray();
	}

	private static string Manifest(int version = 1) =>
		JsonSerializer.Serialize(new BackupManifest(BackupManifest.ExpectedFormat, version, _now, 0), JsonSerializerOptions.Web);

	private static string SettingsJson(string apiKey = "BACKUPKEY") =>
		JsonSerializer.Serialize(new HostSettings { ApiKey = apiKey, WebcamStreamUrl = "http://cam" }, JsonSerializerOptions.Web);

	[Fact]
	public async Task Round_trips_settings_profiles_files_and_their_history()
	{
		Host source = HostIn(_source);
		PrinterProfile mk3s = new() { Id = "mk3s", Name = "MK3S", BedWidth = 250, BedDepth = 210, MaxHeight = 210 };
		await source.Settings.UpdateAsync(s => s with { Profiles = [new PrinterProfile(), mk3s], ActiveProfileId = "mk3s", WebcamStreamUrl = "http://cam" }, Ct);
		await Upload(source, "cube.gcode", "G28\nG1 X10\n");
		await source.Files.SaveAnalysisAsync("cube.gcode", Analysis(layers: 12), Ct);
		await source.Files.RecordPrintAsync("cube.gcode", new PrintRecord(_now.AddMinutes(-5), _now, JobStatus.Completed, TimeSpan.FromMinutes(5), 1.5), Ct);
		await Upload(source, "benchy.gcode", "G28\n");
		File.SetLastWriteTimeUtc(Path.Combine(source.Uploads, "benchy.gcode"), new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Utc));
		HostSettings saved = await source.Settings.GetAsync(Ct);

		Host target = HostIn(_target);
		Result<BackupRestore> result = await Restore(target, await Backup(source));

		Assert.True(result.IsSuccess, result.Error);
		Assert.Equal(_now, result.Value!.CreatedAt);
		Assert.Equal(["benchy.gcode", "cube.gcode"], result.Value.Files.Order());

		HostSettings restored = await target.Settings.GetAsync(Ct);
		Assert.Equal(saved.ApiKey, restored.ApiKey);
		Assert.Equal("mk3s", restored.ActiveProfileId);
		Assert.Equal(mk3s, restored.ActiveProfile);
		Assert.Equal("http://cam", restored.WebcamStreamUrl);
		// A fresh store reads what was written to disk, not the restoring store's cache.
		Assert.Equal("mk3s", (await HostIn(_target).Settings.GetAsync(Ct)).ActiveProfileId);

		GcodeFile cube = (await target.Files.GetAsync("cube.gcode", Ct))!;
		Assert.Equal(12, cube.Analysis?.LayerCount);
		Assert.Equal(1, cube.Prints?.Total);
		Assert.Equal("G28\nG1 X10\n", await File.ReadAllTextAsync(Path.Combine(target.Uploads, "cube.gcode"), Ct));
		GcodeFile benchy = (await target.Files.GetAsync("benchy.gcode", Ct))!;
		Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero), benchy.UploadedAt);
		Assert.Empty(Directory.EnumerateDirectories(target.Uploads));
	}

	[Fact]
	public async Task The_archive_has_a_manifest_and_settings_first()
	{
		Host source = HostIn(_source);
		await Upload(source, "cube.gcode", "G28\n");
		await source.Files.SaveAnalysisAsync("cube.gcode", Analysis(layers: 1), Ct);

		using ZipArchive zip = new(new MemoryStream(await Backup(source)), ZipArchiveMode.Read);

		Assert.Equal(["printdeck-backup.json", "settings.json", "uploads/cube.gcode", "uploads/cube.gcode.meta.json"],
			zip.Entries.Select(e => e.FullName));
		await using Stream manifest = zip.GetEntry("printdeck-backup.json")!.Open();
		BackupManifest read = (await JsonSerializer.DeserializeAsync<BackupManifest>(manifest, JsonSerializerOptions.Web, Ct))!;
		Assert.Null(read.Problem());
		Assert.Equal(_now, read.CreatedAt);
		Assert.Equal(1, read.FileCount);
	}

	[Fact]
	public async Task Other_files_stay_and_same_names_are_overwritten_with_their_own_history()
	{
		Host target = HostIn(_target);
		await Upload(target, "keep.gcode", "G28\n");
		await Upload(target, "cube.gcode", "OLD\n");
		await target.Files.SaveAnalysisAsync("cube.gcode", Analysis(layers: 99), Ct);
		await target.Files.RecordPrintAsync("cube.gcode", new PrintRecord(_now.AddMinutes(-1), _now, JobStatus.Cancelled), Ct);

		Result<BackupRestore> result = await Restore(target,
			Zip(("printdeck-backup.json", Manifest()), ("settings.json", SettingsJson()), ("uploads/cube.gcode", "NEW\n")));

		Assert.True(result.IsSuccess, result.Error);
		Assert.Equal("NEW\n", await File.ReadAllTextAsync(Path.Combine(target.Uploads, "cube.gcode"), Ct));
		GcodeFile cube = (await target.Files.GetAsync("cube.gcode", Ct))!;
		Assert.Null(cube.Analysis);
		Assert.Equal(0, cube.Prints?.Total);
		Assert.NotNull(await target.Files.GetAsync("keep.gcode", Ct));
		Assert.Equal("BACKUPKEY", (await target.Settings.GetAsync(Ct)).ApiKey);
	}

	[Fact]
	public async Task Keeps_the_current_api_key_when_the_backup_has_none()
	{
		Host target = HostIn(_target);
		string key = (await target.Settings.GetAsync(Ct)).ApiKey;

		Result<BackupRestore> result = await Restore(target, Zip(("printdeck-backup.json", Manifest()), ("settings.json", SettingsJson(apiKey: ""))));

		Assert.True(result.IsSuccess, result.Error);
		Assert.Equal(key, (await target.Settings.GetAsync(Ct)).ApiKey);
	}

	[Fact]
	public async Task Sidecars_without_their_file_are_ignored()
	{
		Host target = HostIn(_target);

		Result<BackupRestore> result = await Restore(target,
			Zip(("printdeck-backup.json", Manifest()), ("settings.json", SettingsJson()), ("uploads/gone.gcode.prints.json", "[]")));

		Assert.True(result.IsSuccess, result.Error);
		Assert.Empty(result.Value!.Files);
		Assert.Empty(Directory.EnumerateFileSystemEntries(target.Uploads));
	}

	public static TheoryData<string, byte[]> Refused => new()
	{
		{ ZipBackupArchive.NotABackup, Encoding.UTF8.GetBytes("G28\n") },
		{ ZipBackupArchive.NotABackup, Zip(("settings.json", SettingsJson())) },
		{ ZipBackupArchive.NotABackup, Zip(("printdeck-backup.json", "{nope"), ("settings.json", SettingsJson())) },
		{ "This backup was made by a newer PrintDeck. Update PrintDeck first.", Zip(("printdeck-backup.json", Manifest(version: 2)), ("settings.json", SettingsJson())) },
		{ ZipBackupArchive.Damaged, Zip(("printdeck-backup.json", Manifest())) },
		{ ZipBackupArchive.Damaged, Zip(("printdeck-backup.json", Manifest()), ("settings.json", "null")) },
		{ ZipBackupArchive.InvalidName, Zip(("printdeck-backup.json", Manifest()), ("settings.json", SettingsJson()), ("uploads/../evil.gcode", "M112\n")) },
		{ ZipBackupArchive.InvalidName, Zip(("printdeck-backup.json", Manifest()), ("settings.json", SettingsJson()), ("uploads/sub/cube.gcode", "G28\n")) },
	};

	[Theory]
	[MemberData(nameof(Refused))]
	public async Task Refused_archives_change_nothing(string error, byte[] archive)
	{
		Host target = HostIn(_target);
		await Upload(target, "cube.gcode", "OLD\n");
		string before = JsonSerializer.Serialize(await target.Settings.GetAsync(Ct));

		Result<BackupRestore> result = await Restore(target, archive);

		Assert.Equal(error, result.Error);
		Assert.Equal(before, JsonSerializer.Serialize(await HostIn(_target).Settings.GetAsync(Ct)));
		Assert.Equal("OLD\n", await File.ReadAllTextAsync(Path.Combine(target.Uploads, "cube.gcode"), Ct));
		Assert.False(File.Exists(Path.Combine(_target.Path, "evil.gcode")));
		Assert.Empty(Directory.EnumerateDirectories(target.Uploads));
		Assert.Empty(Directory.EnumerateFiles(_target.Path, "restore-*"));
	}

	[Fact]
	public async Task A_damaged_file_in_the_archive_changes_nothing()
	{
		Host target = HostIn(_target);
		await Upload(target, "cube.gcode", "OLD\n");
		string content = string.Concat(Enumerable.Repeat("G1 X10 Y10 E0.5\n", 2000));
		byte[] zip = Zip(("printdeck-backup.json", Manifest()), ("settings.json", SettingsJson()), ("uploads/a.gcode", "NEW\n"), ("uploads/cube.gcode", content));
		// Flip a byte in the compressed data of the last entry; its CRC no longer matches.
		using (ZipArchive read = new(new MemoryStream(zip), ZipArchiveMode.Read))
			Assert.Equal("uploads/cube.gcode", read.Entries[^1].FullName);
		int central = FindCentralDirectory(zip);
		zip[central - 24] ^= 0xFF; // well inside the deflate data, not its ignored padding

		Result<BackupRestore> result = await Restore(target, zip);

		Assert.Equal(ZipBackupArchive.Damaged, result.Error);
		Assert.Equal("OLD\n", await File.ReadAllTextAsync(Path.Combine(target.Uploads, "cube.gcode"), Ct));
		Assert.False(File.Exists(Path.Combine(target.Uploads, "a.gcode")));
		Assert.NotEqual("BACKUPKEY", (await target.Settings.GetAsync(Ct)).ApiKey);
		Assert.Empty(Directory.EnumerateDirectories(target.Uploads));
	}

	/// <summary>Where the central directory starts; the bytes before it are the last entry's compressed data.</summary>
	private static int FindCentralDirectory(byte[] zip)
	{
		for (int i = 0; i + 3 < zip.Length; i++)
			if (zip[i] == 0x50 && zip[i + 1] == 0x4b && zip[i + 2] == 0x01 && zip[i + 3] == 0x02) return i;
		throw new InvalidOperationException("No central directory.");
	}
}
