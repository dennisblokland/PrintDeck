using Mediator;

using PrintDeck.Application.Features.SdCard.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Handlers;

public sealed class InitSdCardHandler(IPrinterSession session) : ICommandHandler<InitSdCardCommand, Result>
{
	public ValueTask<Result> Handle(InitSdCardCommand c, CancellationToken ct) => ValueTask.FromResult(session.InitSdCard());
}
