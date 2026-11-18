using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Files.Commands;

/// <summary>Moves and/or renames a stored file: <paramref name="Destination"/> is its new full path, e.g. "parts/benchy-v2.gcode".</summary>
public sealed record MoveGcodeCommand(string FileName, string Destination) : ICommand<Result>;
