using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Files;

/// <summary>Keeps the last few parsed toolpaths; an entry is reused only for the same upload.</summary>
public sealed class ToolpathCache
{
	private const int Capacity = 3;
	private readonly Lock _gate = new();
	private readonly LinkedList<(string Name, DateTimeOffset UploadedAt, long Size, Task<GcodeToolpath> Task)> _entries = new();

	public async Task<GcodeToolpath> GetAsync(GcodeFile file, Func<Stream> open, CancellationToken ct)
	{
		Task<GcodeToolpath> task;
		lock (_gate)
		{
			var node = _entries.First;
			while (node is not null && !(node.Value.Name == file.Name && node.Value.UploadedAt == file.UploadedAt && node.Value.Size == file.SizeBytes))
				node = node.Next;

			if (node is not null && !node.Value.Task.IsFaulted && !node.Value.Task.IsCanceled)
			{
				_entries.Remove(node);
				_entries.AddFirst(node);
				task = node.Value.Task;
			}
			else
			{
				if (node is not null) _entries.Remove(node);
				// Parse independently of the first caller's cancellation so others can share it.
				task = Task.Run(async () =>
				{
					await using Stream stream = open();
					return await GcodeToolpathParser.ParseAsync(stream, CancellationToken.None);
				}, CancellationToken.None);
				_entries.AddFirst((file.Name, file.UploadedAt, file.SizeBytes, task));
				while (_entries.Count > Capacity) _entries.RemoveLast();
			}
		}
		return await task.WaitAsync(ct);
	}

	public void Invalidate(string name)
	{
		lock (_gate)
		{
			for (var node = _entries.First; node is not null;)
			{
				var next = node.Next;
				if (node.Value.Name == name) _entries.Remove(node);
				node = next;
			}
		}
	}
}
