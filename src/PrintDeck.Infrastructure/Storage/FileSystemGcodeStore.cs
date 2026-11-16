using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Files;

namespace PrintDeck.Infrastructure.Storage;

/// <summary>
/// G-code files on disk; analysis kept in a sidecar "&lt;name&gt;.meta.json" and finished prints in "&lt;name&gt;.prints.json".
/// </summary>
public sealed class FileSystemGcodeStore(IOptions<StorageOptions> options) : IGcodeFileStore
{
	private const string _metaSuffix = ".meta.json";
	private const string _printsSuffix = ".prints.json";
	private const int _maxPrints = 100;
	private readonly SemaphoreSlim _historyLock = new(1, 1);
	// Status as text ("Completed"), so reordering JobStatus never rewrites history.
	private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
	private readonly string _root = Directory.CreateDirectory(options.Value.UploadsDirectory).FullName;

	private string PathOf(string name)
	{
		string full = Path.GetFullPath(Path.Combine(_root, name));
		if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
			throw new ArgumentException("Invalid file name.", nameof(name));
		return full;
	}

	public async Task<IReadOnlyList<GcodeFile>> ListAsync(CancellationToken ct)
	{
		List<GcodeFile> result = new();
		foreach (string? path in Directory.EnumerateFiles(_root).Where(IsGcode))
			result.Add(await LoadAsync(new FileInfo(path), ct));
		return result;
	}

	public async Task<GcodeFile?> GetAsync(string name, CancellationToken ct)
	{
		FileInfo info = new(PathOf(name));
		return info.Exists ? await LoadAsync(info, ct) : null;
	}

	public async Task<GcodeFile> SaveAsync(string name, Stream content, CancellationToken ct)
	{
		string path = PathOf(name);
		string temp = path + ".part";
		await using (FileStream file = File.Create(temp))
			await content.CopyToAsync(file, ct);
		File.Move(temp, path, overwrite: true);
		File.Delete(path + _metaSuffix);
		return (await GetAsync(name, ct))!;
	}

	public async Task SaveAnalysisAsync(string name, GcodeAnalysis analysis, CancellationToken ct)
	{
		await using FileStream meta = File.Create(PathOf(name) + _metaSuffix);
		await JsonSerializer.SerializeAsync(meta, analysis, _json, ct);
	}

	public Stream OpenRead(string name) =>
		new FileStream(PathOf(name), FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);

	public Task<bool> DeleteAsync(string name, CancellationToken ct)
	{
		string path = PathOf(name);
		if (!File.Exists(path)) return Task.FromResult(false);
		File.Delete(path);
		File.Delete(path + _metaSuffix);
		File.Delete(path + _printsSuffix);
		return Task.FromResult(true);
	}

	public async Task RecordPrintAsync(string name, PrintRecord print, CancellationToken ct)
	{
		string path = PathOf(name);
		await _historyLock.WaitAsync(ct);
		try
		{
			if (!File.Exists(path)) return;
			List<PrintRecord> prints = [.. await ReadPrintsAsync(path, ct), print];
			string temp = path + _printsSuffix + ".part";
			await using (FileStream s = File.Create(temp))
				await JsonSerializer.SerializeAsync(s, prints.TakeLast(_maxPrints).ToList(), _json, ct);
			File.Move(temp, path + _printsSuffix, overwrite: true);
		}
		finally
		{
			_historyLock.Release();
		}
	}

	public async Task<IReadOnlyList<PrintHistoryEntry>> ListPrintsAsync(CancellationToken ct)
	{
		List<PrintHistoryEntry> result = [];
		foreach (string sidecar in Directory.EnumerateFiles(_root, "*" + _printsSuffix))
		{
			string path = sidecar[..^_printsSuffix.Length];
			if (!File.Exists(path)) continue; // left behind by a file deleted outside PrintDeck
			string name = Path.GetFileName(path);
			foreach (PrintRecord print in await ReadPrintsAsync(path, ct))
				result.Add(new PrintHistoryEntry(name, print));
		}
		return result;
	}

	private static bool IsGcode(string path) =>
		!path.EndsWith(_metaSuffix, StringComparison.Ordinal)
		&& !path.EndsWith(_printsSuffix, StringComparison.Ordinal)
		&& !path.EndsWith(".part", StringComparison.Ordinal);

	private static async Task<IReadOnlyList<PrintRecord>> ReadPrintsAsync(string path, CancellationToken ct)
	{
		string file = path + _printsSuffix;
		if (!File.Exists(file)) return [];
		try
		{
			await using FileStream s = File.OpenRead(file);
			return await JsonSerializer.DeserializeAsync<List<PrintRecord>>(s, _json, ct) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static async Task<GcodeFile> LoadAsync(FileInfo info, CancellationToken ct)
	{
		GcodeAnalysis? analysis = null;
		string meta = info.FullName + _metaSuffix;
		if (File.Exists(meta))
		{
			try
			{
				await using FileStream s = File.OpenRead(meta);
				analysis = await JsonSerializer.DeserializeAsync<GcodeAnalysis>(s, _json, ct);
			}
			catch (JsonException) { /* re-analysed on next upload */ }
		}
		PrintStats prints = PrintStats.From(await ReadPrintsAsync(info.FullName, ct));
		return new GcodeFile(info.Name, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), analysis, prints);
	}
}
