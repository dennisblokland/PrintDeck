using System.Text;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class InMemoryGcodeFileStore : IGcodeFileStore
{
	private readonly Dictionary<string, (byte[] Content, DateTimeOffset UploadedAt, GcodeAnalysis? Analysis)> _files = new();

	private readonly Dictionary<string, List<PrintRecord>> _prints = new();

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

	public IReadOnlyList<PrintRecord> PrintsOf(string name) => _prints.TryGetValue(name, out List<PrintRecord>? list) ? list : [];

	private GcodeFile ToFile(string name)
	{
		(byte[] content, DateTimeOffset uploadedAt, GcodeAnalysis? analysis) = _files[name];
		return new GcodeFile(name, content.Length, uploadedAt, analysis, PrintStats.From(PrintsOf(name)));
	}
}
