using Mediator;

using PrintDeck.Application.Features.SdCard.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Handlers;

public sealed class ReleaseSdCardHandler(IPrinterSession session) : ICommandHandler<ReleaseSdCardCommand, Result>
{
	public ValueTask<Result> Handle(ReleaseSdCardCommand c, CancellationToken ct) => ValueTask.FromResult(session.ReleaseSdCard());
}
