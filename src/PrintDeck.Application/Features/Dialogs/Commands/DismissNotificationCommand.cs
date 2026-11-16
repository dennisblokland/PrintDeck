using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Dialogs.Commands;

/// <summary>Clears the printer message for everyone.</summary>
public sealed record DismissNotificationCommand : ICommand<Result>;
