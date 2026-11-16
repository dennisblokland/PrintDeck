using Mediator;

using PrintDeck.Application.Features.SdCard.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Handlers;

public sealed class RefreshSdCardHandler(IPrinterSession session) : ICommandHandler<RefreshSdCardCommand, Result>
{
	public ValueTask<Result> Handle(RefreshSdCardCommand c, CancellationToken ct) => ValueTask.FromResult(session.RefreshSdFiles());
}
