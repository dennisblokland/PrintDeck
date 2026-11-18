using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

/// <summary>Only empty folders, so a slip of the mouse can't delete a whole collection of prints.</summary>
public sealed class DeleteFolderHandler(IGcodeFileStore files, PrinterEventHub events) : ICommandHandler<DeleteFolderCommand, Result>
{
	public async ValueTask<Result> Handle(DeleteFolderCommand c, CancellationToken ct)
	{
		if (FolderRules.Folder(c.Path) is not { } path) return Result.Failure(FolderRules.InvalidFolder);
		IReadOnlyList<string> folders = files.ListFolders();
		if (!folders.Contains(path)) return Result.Failure(FolderRules.FolderNotFound);
		if (folders.Any(f => f != path && FilePath.IsWithin(f, path)) || (await files.ListAsync(ct)).Any(f => FilePath.IsWithin(f.Name, path)))
			return Result.Failure("The folder isn't empty. Move or delete what's in it first.");

		if (!files.DeleteFolder(path)) return Result.Failure("The folder isn't empty. Move or delete what's in it first.");
		events.Publish(PrinterChange.Files);
		return Result.Success();
	}
}
