using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class UploadGcodeHandler(IGcodeFileStore files, PrinterEventHub events)
	: ICommandHandler<UploadGcodeCommand, Result<GcodeFile>>
{
	private static readonly string[] _extensions = [".gcode", ".gco", ".g", ".bgcode"];

	public async ValueTask<Result<GcodeFile>> Handle(UploadGcodeCommand c, CancellationToken ct)
	{
		string? name = SanitizeName(c.FileName);
		if (name is null) return Result.Failure<GcodeFile>("Invalid file name.");
		if (!_extensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
			return Result.Failure<GcodeFile>("Only G-code files (.gcode, .gco, .g) can be uploaded.");

		GcodeFile saved = await files.SaveAsync(name, c.Content, ct);
		await using (Stream stream = files.OpenRead(name))
		{
			GcodeAnalysis analysis = await GcodeAnalyzer.AnalyzeAsync(stream, ct);
			await files.SaveAnalysisAsync(name, analysis, ct);
			saved = saved with { Analysis = analysis };
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
