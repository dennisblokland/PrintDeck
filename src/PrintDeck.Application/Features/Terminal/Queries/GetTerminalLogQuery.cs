using Mediator;

using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Application.Features.Terminal.Handlers;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Features.Terminal.Queries;

public sealed record GetTerminalLogQuery(int Last = 500, bool HideTemperature = true) : IQuery<IReadOnlyList<TerminalEntry>>;
