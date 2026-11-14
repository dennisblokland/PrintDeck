using Mediator;

using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Application.Features.Connection.Handlers;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Features.Connection.Queries;

public sealed record GetPrinterStatusQuery : IQuery<PrinterStatusDto>;
