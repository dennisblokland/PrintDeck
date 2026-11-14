using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Application.Features.Connection.Handlers;
using PrintDeck.Application.Features.Connection.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Connection.Commands;

public sealed record DisconnectPrinterCommand : ICommand<Result>;
