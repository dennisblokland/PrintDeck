using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.SdCard.Commands;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Handlers;

public sealed class StartSdPrintHandler(IPrinterSession session, ISettingsStore settings) : ICommandHandler<StartSdPrintCommand, Result>
{
	public async ValueTask<Result> Handle(StartSdPrintCommand c, CancellationToken ct) =>
		session.StartSdPrint(c.Path, PrintScripts.From(await settings.GetAsync(ct)));
}
