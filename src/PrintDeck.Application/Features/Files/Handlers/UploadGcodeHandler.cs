using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Application.Files.Thumbnails;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class UploadGcodeHandler(IGcodeFileStore files, PrinterEventHub events)
	: ICommandHandler<UploadGcodeCommand, Result<GcodeFile>>
{
	private static readonly string[] _extensions = [".gcode", ".gco", ".g", ".bgcode"];

	public const string NotGcode = "Only G-code files (.gcode, .gco, .g) can be uploaded.";

	public static bool IsGcodeName(string name) => _extensions.Contains(Path.GetExtension(name).ToLowerInvariant());

	public async ValueTask<Result<GcodeFile>> Handle(UploadGcodeCommand c, CancellationToken ct)
	{
		string? fileName = SanitizeName(c.FileName);
		if (fileName is null) return Result.Failure<GcodeFile>("Invalid file name.");
		if (!IsGcodeName(fileName)) return Result.Failure<GcodeFile>(NotGcode);
		if (FilePath.Normalize(c.Folder) is not { } folder) return Result.Failure<GcodeFile>(FolderRules.InvalidFolder);
		string name = FilePath.Combine(folder, fileName);
		if (files.ListFolders().Contains(name)) return Result.Failure<GcodeFile>("A folder with that name already exists.");

		GcodeFile saved = await files.SaveAsync(name, c.Content, ct);
		await using (Stream stream = files.OpenRead(name))
		{
			GcodeAnalysis analysis = await GcodeAnalyzer.AnalyzeAsync(stream, ct);
			await files.SaveAnalysisAsync(name, analysis, ct);
			saved = saved with { Analysis = analysis };
		}
		await using (Stream stream = files.OpenRead(name))
		{
			if (await GcodeThumbnailReader.ReadAsync(stream, ct) is { } thumbnail)
			{
				await files.SaveThumbnailAsync(name, thumbnail, ct);
				saved = saved with { HasThumbnail = true };
			}
		}

		events.Publish(PrinterChange.Files);
		return Result.Success(saved);
	}

	internal static string? SanitizeName(string raw)
	{
		string name = Path.GetFileName(raw.Replace('\\', '/')).Trim();
		foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
		return name.Length == 0 || name.StartsWith('.') ? null : name;
	}
}
