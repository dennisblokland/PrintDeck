using Mediator;

using PrintDeck.Application.Features.Dialogs.Dtos;
using PrintDeck.Application.Features.Dialogs.Queries;
using PrintDeck.Application.Printer.Interfaces;

namespace PrintDeck.Application.Features.Dialogs.Handlers;

public sealed class GetPrinterDialogHandler(IPrinterSession session) : IQueryHandler<GetPrinterDialogQuery, PrinterDialogDto>
{
	public ValueTask<PrinterDialogDto> Handle(GetPrinterDialogQuery query, CancellationToken ct) =>
		ValueTask.FromResult(new PrinterDialogDto(session.Prompt, session.Notification, session.WaitingForUser));
}
