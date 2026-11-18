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

public sealed class MoveFolderHandler(IGcodeFileStore files, IPrinterSession session, PrinterEventHub events, ToolpathCache toolpaths)
	: ICommandHandler<MoveFolderCommand, Result>
{
	public async ValueTask<Result> Handle(MoveFolderCommand c, CancellationToken ct)
	{
		if (FolderRules.Folder(c.Path) is not { } from || FolderRules.Folder(c.Destination) is not { } to)
			return Result.Failure(FolderRules.InvalidFolder);
		if (from == to) return Result.Success();
		if (FilePath.IsWithin(to, from)) return Result.Failure("A folder can't go inside itself.");

		IReadOnlyList<string> folders = files.ListFolders();
		if (!folders.Contains(from)) return Result.Failure(FolderRules.FolderNotFound);
		if (folders.Contains(to) || await files.GetAsync(to, ct) is not null) return Result.Failure(FolderRules.FolderExists);
		if (FolderRules.ActiveFile(session) is { } active && FilePath.IsWithin(active, from))
			return Result.Failure("Can't move a folder while one of its files is printing.");

		IReadOnlyList<GcodeFile> moved = [.. (await files.ListAsync(ct)).Where(f => FilePath.IsWithin(f.Name, from))];
		if (!files.MoveFolder(from, to)) return Result.Failure(FolderRules.FolderNotFound);
		foreach (GcodeFile f in moved) toolpaths.Invalidate(f.Name);
		events.Publish(PrinterChange.Files);
		return Result.Success();
	}
}
