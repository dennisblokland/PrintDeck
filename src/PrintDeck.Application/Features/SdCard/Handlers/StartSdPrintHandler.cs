using Mediator;

using PrintDeck.Application.Features.SdCard.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Handlers;

public sealed class StartSdPrintHandler(IPrinterSession session) : ICommandHandler<StartSdPrintCommand, Result>
{
	public ValueTask<Result> Handle(StartSdPrintCommand c, CancellationToken ct) => ValueTask.FromResult(session.StartSdPrint(c.Path));
}
