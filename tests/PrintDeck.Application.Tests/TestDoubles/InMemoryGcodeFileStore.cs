using System.Text;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class InMemoryGcodeFileStore : IGcodeFileStore
{
	private readonly Dictionary<string, (byte[] Content, DateTimeOffset UploadedAt, GcodeAnalysis? Analysis)> _files = new();

	private readonly Dictionary<string, List<PrintRecord>> _prints = new();

	public Dictionary<string, GcodeThumbnail> Thumbnails { get; } = new();

	public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
	public int Opens { get; private set; }

	public void Add(string name, string content, DateTimeOffset? uploadedAt = null, GcodeAnalysis? analysis = null) =>
		_files[name] = (Encoding.UTF8.GetBytes(content), uploadedAt ?? Now, analysis);

	public bool Contains(string name) => _files.ContainsKey(name);

	public Task<IReadOnlyList<GcodeFile>> ListAsync(CancellationToken ct) =>
		Task.FromResult<IReadOnlyList<GcodeFile>>(_files.Keys.Select(ToFile).ToList());

	public Task<GcodeFile?> GetAsync(string name, CancellationToken ct) =>
		Task.FromResult(_files.ContainsKey(name) ? ToFile(name) : null);

	public async Task<GcodeFile> SaveAsync(string name, Stream content, CancellationToken ct)
	{
		using MemoryStream copy = new();
		await content.CopyToAsync(copy, ct);
		_files[name] = (copy.ToArray(), Now, null);
		Thumbnails.Remove(name);
		return ToFile(name);
	}

	public Task SaveAnalysisAsync(string name, GcodeAnalysis analysis, CancellationToken ct)
	{
		_files[name] = _files[name] with { Analysis = analysis };
		return Task.CompletedTask;
	}

	public Stream OpenRead(string name)
	{
		Opens++;
		return new MemoryStream(_files[name].Content, writable: false);
	}

	public Task<bool> DeleteAsync(string name, CancellationToken ct)
	{
		_prints.Remove(name);
		Thumbnails.Remove(name);
		return Task.FromResult(_files.Remove(name));
	}

	public Task RecordPrintAsync(string name, PrintRecord print, CancellationToken ct)
	{
		if (_files.ContainsKey(name))
		{
			if (!_prints.TryGetValue(name, out List<PrintRecord>? list)) _prints[name] = list = [];
			list.Add(print);
		}
		return Task.CompletedTask;
	}

	public Task SaveThumbnailAsync(string name, GcodeThumbnail thumbnail, CancellationToken ct)
	{
		if (_files.ContainsKey(name)) Thumbnails[name] = thumbnail;
		return Task.CompletedTask;
	}

	public (Stream Content, string ContentType)? OpenThumbnail(string name) =>
		Thumbnails.TryGetValue(name, out GcodeThumbnail? t) ? (new MemoryStream(t.Data, writable: false), t.ContentType) : null;

	public Task<IReadOnlyList<PrintHistoryEntry>> ListPrintsAsync(CancellationToken ct) =>
		Task.FromResult<IReadOnlyList<PrintHistoryEntry>>(
			_prints.SelectMany(f => f.Value.Select(p => new PrintHistoryEntry(f.Key, p))).ToList());

	public HashSet<string> Folders { get; } = new(StringComparer.Ordinal);

	/// <summary>Folders made explicitly plus every folder a file sits in, with their parents.</summary>
	public IReadOnlyList<string> ListFolders() =>
		Folders.Concat(_files.Keys.Select(FilePath.Folder))
			.SelectMany(Parents)
			.Distinct()
			.Order(StringComparer.OrdinalIgnoreCase)
			.ToList();

	private static IEnumerable<string> Parents(string folder)
	{
		for (string f = folder; f.Length > 0; f = FilePath.Folder(f)) yield return f;
	}

	public bool CreateFolder(string path) => !ListFolders().Contains(path) && Folders.Add(path);

	public Task<bool> MoveAsync(string from, string to, CancellationToken ct)
	{
		if (!_files.Remove(from, out (byte[] Content, DateTimeOffset UploadedAt, GcodeAnalysis? Analysis) file)) return Task.FromResult(false);
		_files[to] = file;
		if (_prints.Remove(from, out List<PrintRecord>? prints)) _prints[to] = prints;
		if (Thumbnails.Remove(from, out GcodeThumbnail? thumbnail)) Thumbnails[to] = thumbnail;
		return Task.FromResult(true);
	}

	public bool MoveFolder(string from, string to)
	{
		if (!ListFolders().Contains(from)) return false;
		string Moved(string path) => to + path[from.Length..];
		foreach (string f in Folders.Where(f => FilePath.IsWithin(f, from)).ToList()) { Folders.Remove(f); Folders.Add(Moved(f)); }
		Folders.Add(to);
		foreach (string name in _files.Keys.Where(n => FilePath.IsWithin(n, from)).ToList())
			MoveAsync(name, Moved(name), CancellationToken.None);
		return true;
	}

	public bool DeleteFolder(string path)
	{
		if (!ListFolders().Contains(path) || ListFolders().Any(f => f != path && FilePath.IsWithin(f, path))
			|| _files.Keys.Any(n => FilePath.IsWithin(n, path))) return false;
		Folders.Remove(path);
		return true;
	}

	public IReadOnlyList<PrintRecord> PrintsOf(string name) => _prints.TryGetValue(name, out List<PrintRecord>? list) ? list : [];

	private GcodeFile ToFile(string name)
	{
		(byte[] content, DateTimeOffset uploadedAt, GcodeAnalysis? analysis) = _files[name];
		return new GcodeFile(name, content.Length, uploadedAt, analysis, PrintStats.From(PrintsOf(name)), Thumbnails.ContainsKey(name));
	}
}
