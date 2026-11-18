using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files;

/// <summary>Checks and messages shared by the folder and move commands.</summary>
internal static class FolderRules
{
	public const string InvalidFolder = "Invalid folder name.";
	public const string FolderNotFound = "Folder not found.";
	public const string FolderExists = "A folder with that name already exists.";

	/// <summary>A folder path that isn't the top level, or null.</summary>
	public static string? Folder(string? raw) => FilePath.Normalize(raw) is { Length: > 0 } path ? path : null;

	/// <summary>The file the printer is working through now (streaming or copying to SD), if any.</summary>
	public static string? ActiveFile(IPrinterSession session) => session.CurrentJob is { IsActive: true } job ? job.FileName : null;
}
