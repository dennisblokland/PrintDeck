using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Terminal.Commands;

/// <summary>Adds an info line to the terminal, such as a module saying what it did.</summary>
public sealed record AddTerminalNoteCommand(string Text) : ICommand<Result>;
