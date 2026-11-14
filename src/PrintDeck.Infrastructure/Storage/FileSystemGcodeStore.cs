using System.Text.Json;

using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Files;

namespace PrintDeck.Infrastructure.Storage;

/// <summary>G-code files on disk; analysis kept in a sidecar "&lt;name&gt;.meta.json".</summary>
public sealed class FileSystemGcodeStore(IOptions<StorageOptions> options) : IGcodeFileStore
{
	private const string _metaSuffix = ".meta.json";
	private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
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
		foreach (string? path in Directory.EnumerateFiles(_root).Where(p => !p.EndsWith(_metaSuffix, StringComparison.Ordinal)))
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
		return Task.FromResult(true);
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
		return new GcodeFile(info.Name, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), analysis);
	}
}
