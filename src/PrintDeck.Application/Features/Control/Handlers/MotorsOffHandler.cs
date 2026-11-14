using System.Globalization;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;

namespace PrintDeck.Application.Features.Control.Handlers;

public sealed class MotorsOffHandler(IPrinterSession session) : ICommandHandler<MotorsOffCommand, Result>
{
	public ValueTask<Result> Handle(MotorsOffCommand c, CancellationToken ct) =>
		ValueTask.FromResult(Guard.CanMove(session) is { IsSuccess: false } b ? b : session.Enqueue(["M84"]));
}
