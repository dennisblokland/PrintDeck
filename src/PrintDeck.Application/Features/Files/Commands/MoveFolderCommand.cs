using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Files.Commands;

/// <summary>Moves and/or renames a folder with everything in it: <paramref name="Destination"/> is its new full path.</summary>
public sealed record MoveFolderCommand(string Path, string Destination) : ICommand<Result>;
