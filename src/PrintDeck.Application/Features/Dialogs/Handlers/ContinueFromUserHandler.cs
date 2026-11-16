using Mediator;

using PrintDeck.Application.Features.Dialogs.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Dialogs.Handlers;

public sealed class ContinueFromUserHandler(IPrinterSession session) : ICommandHandler<ContinueFromUserCommand, Result>
{
	public ValueTask<Result> Handle(ContinueFromUserCommand c, CancellationToken ct) => ValueTask.FromResult(session.ContinueFromUser());
}
