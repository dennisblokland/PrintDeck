using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Dialogs.Commands;

/// <summary>Releases an M0/M1/M600 wait (M108).</summary>
public sealed record ContinueFromUserCommand : ICommand<Result>;
