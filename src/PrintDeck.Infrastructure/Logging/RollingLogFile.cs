using System.Text;
using System.Threading.Channels;

namespace PrintDeck.Infrastructure.Logging;

/// <summary>
/// Appends lines to a file from a background writer, so callers never wait on the disk.
/// When the file passes <paramref name="maxBytes"/> it becomes <c>name.1</c> (older ones shift up to <c>name.{keep}</c>).
/// </summary>
public sealed class RollingLogFile(string path, long maxBytes, int keep) : IAsyncDisposable
{
	private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new() { SingleReader = true });
	private readonly SemaphoreSlim _flushed = new(0);
	private readonly Lock _start = new();
	private Task? _writer;

	public string Path => path;

	public void Write(string line)
	{
		// Empty lines are the flush marker, so they never reach the file.
		if (line.Length > 0 && _lines.Writer.TryWrite(line)) EnsureWriter();
	}

	/// <summary>Waits until everything written before this call is on disk.</summary>
	public async Task FlushAsync(CancellationToken ct = default)
	{
		if (!_lines.Writer.TryWrite("")) return; // disposed
		EnsureWriter();
		await _flushed.WaitAsync(ct);
	}

	private void EnsureWriter()
	{
		if (_writer is not null) return;
		lock (_start) _writer ??= Task.Run(WriteLoopAsync);
	}

	private async Task WriteLoopAsync()
	{
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
		FileStream? file = null;
		StreamWriter? writer = null;
		int flushes = 0;
		try
		{
			while (await _lines.Reader.WaitToReadAsync())
			{
				while (_lines.Reader.TryRead(out string? line))
				{
					if (line.Length == 0)
					{
						flushes++; // everything before the marker is already in the writer
						continue;
					}
					if (writer is null || file!.Length >= maxBytes)
					{
						if (writer is not null)
						{
							await writer.DisposeAsync();
							Roll();
						}
						file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
						writer = new StreamWriter(file, new UTF8Encoding(false));
					}
					await writer.WriteLineAsync(line);
				}
				if (writer is not null) await writer.FlushAsync();
				if (flushes > 0) _flushed.Release(flushes);
				flushes = 0;
			}
		}
		finally
		{
			if (writer is not null) await writer.DisposeAsync();
			if (flushes > 0) _flushed.Release(flushes);
		}
	}

	/// <summary>name → name.1 → name.2 … the oldest beyond <c>keep</c> is dropped.</summary>
	private void Roll()
	{
		File.Delete($"{path}.{keep}");
		for (int i = keep - 1; i >= 1; i--)
			if (File.Exists($"{path}.{i}")) File.Move($"{path}.{i}", $"{path}.{i + 1}");
		File.Move(path, $"{path}.1");
	}

	public async ValueTask DisposeAsync()
	{
		_lines.Writer.TryComplete();
		if (_writer is not null) await _writer;
	}
}
