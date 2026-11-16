using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Dialogs.Commands;

/// <summary>Answers the firmware's prompt; <paramref name="Choice"/> is the index of the button.</summary>
public sealed record AnswerPromptCommand(int Choice) : ICommand<Result>;
