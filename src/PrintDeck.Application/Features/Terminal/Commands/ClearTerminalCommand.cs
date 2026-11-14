using Mediator;

using PrintDeck.Application.Features.Terminal.Handlers;
using PrintDeck.Application.Features.Terminal.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Features.Terminal.Commands;

public sealed record ClearTerminalCommand : ICommand<Result>;
