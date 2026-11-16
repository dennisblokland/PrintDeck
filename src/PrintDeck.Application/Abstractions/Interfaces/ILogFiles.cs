using PrintDeck.Domain.Logs;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>The files in PrintDeck's log directory.</summary>
public interface ILogFiles
{
	IReadOnlyList<LogFile> List();

	/// <summary>Opens a listed file for reading while it may still be written; null for any other name.</summary>
	Stream? OpenRead(string name);
}
