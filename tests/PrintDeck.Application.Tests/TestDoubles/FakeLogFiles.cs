using System.Text;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Logs;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeLogFiles : ILogFiles
{
	private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

	public FakeLogFiles With(string name, string content)
	{
		_files[name] = content;
		return this;
	}

	public IReadOnlyList<LogFile> List() =>
		_files.Select(f => new LogFile(f.Key, Encoding.UTF8.GetByteCount(f.Value), DateTimeOffset.UnixEpoch)).ToList();

	public Stream? OpenRead(string name) =>
		_files.TryGetValue(name, out string? content) ? new MemoryStream(Encoding.UTF8.GetBytes(content)) : null;
}
