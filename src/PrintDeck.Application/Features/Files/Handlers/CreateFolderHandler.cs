using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class CreateFolderHandler(IGcodeFileStore files, PrinterEventHub events) : ICommandHandler<CreateFolderCommand, Result>
{
	public ValueTask<Result> Handle(CreateFolderCommand c, CancellationToken ct)
	{
		if (FolderRules.Folder(c.Path) is not { } path) return ValueTask.FromResult(Result.Failure(FolderRules.InvalidFolder));
		if (!files.CreateFolder(path)) return ValueTask.FromResult(Result.Failure(FolderRules.FolderExists));
		events.Publish(PrinterChange.Files);
		return ValueTask.FromResult(Result.Success());
	}
}
