using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Files.Commands;

/// <summary>Creates a folder (and any missing parents) for stored files.</summary>
public sealed record CreateFolderCommand(string Path) : ICommand<Result>;
