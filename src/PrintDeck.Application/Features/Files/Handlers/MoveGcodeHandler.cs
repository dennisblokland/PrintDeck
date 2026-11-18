using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class MoveGcodeHandler(IGcodeFileStore files, IPrinterSession session, PrinterEventHub events, ToolpathCache toolpaths)
	: ICommandHandler<MoveGcodeCommand, Result>
{
	public async ValueTask<Result> Handle(MoveGcodeCommand c, CancellationToken ct)
	{
		if (FilePath.Normalize(c.Destination) is not { Length: > 0 } to || FilePath.Name(to).Length == 0)
			return Result.Failure("Invalid file name.");
		if (!UploadGcodeHandler.IsGcodeName(to)) return Result.Failure(UploadGcodeHandler.NotGcode);
		if (to == c.FileName) return Result.Success();
		if (FolderRules.ActiveFile(session) == c.FileName) return Result.Failure("Can't move the file that is printing.");
		if (await files.GetAsync(c.FileName, ct) is null) return Result.Failure("File not found.");
		if (await files.GetAsync(to, ct) is not null || files.ListFolders().Contains(to))
			return Result.Failure("A file with that name already exists there.");

		if (!await files.MoveAsync(c.FileName, to, ct)) return Result.Failure("File not found.");
		toolpaths.Invalidate(c.FileName);
		events.Publish(PrinterChange.Files);
		return Result.Success();
	}
}
