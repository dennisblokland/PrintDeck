using Mediator;

using PrintDeck.Application.Features.Dialogs.Dtos;

namespace PrintDeck.Application.Features.Dialogs.Queries;

public sealed record GetPrinterDialogQuery : IQuery<PrinterDialogDto>;
