using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Logs;
using PrintDeck.Infrastructure.Storage;

namespace PrintDeck.Infrastructure.Logging;

/// <summary>Lists and opens <c>*.log</c> files (and their rolled-over <c>.1</c>, <c>.2</c> copies) in the logs directory.</summary>
public sealed class LogDirectoryFiles(IOptions<StorageOptions> storage) : ILogFiles
{
	private string Directory => storage.Value.LogsDirectory;

	public IReadOnlyList<LogFile> List()
	{
		DirectoryInfo dir = new(Directory);
		if (!dir.Exists) return [];
		return dir.EnumerateFiles()
			.Where(f => IsLogName(f.Name))
			.Select(f => new LogFile(f.Name, f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))
			.OrderBy(f => f.Name, StringComparer.Ordinal)
			.ToList();
	}

	public Stream? OpenRead(string name)
	{
		// Only names that are listed: no paths, no "..", nothing outside the logs directory.
		if (!List().Any(f => f.Name == name)) return null;
		try
		{
			return new FileStream(Path.Combine(Directory, name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		}
		catch (FileNotFoundException)
		{
			return null; // rolled over in between
		}
	}

	private static bool IsLogName(string name)
	{
		if (name.EndsWith(".log", StringComparison.Ordinal)) return true;
		int dot = name.LastIndexOf('.');
		return dot > 0 && name[..dot].EndsWith(".log", StringComparison.Ordinal) && name[(dot + 1)..].All(char.IsAsciiDigit);
	}
}
