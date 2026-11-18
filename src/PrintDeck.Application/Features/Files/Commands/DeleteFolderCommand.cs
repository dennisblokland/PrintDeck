using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Files.Commands;

/// <summary>Deletes an empty folder.</summary>
public sealed record DeleteFolderCommand(string Path) : ICommand<Result>;
