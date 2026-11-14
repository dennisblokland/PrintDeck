using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Jobs.Handlers;

public sealed class PauseJobHandler(IPrinterSession session) : ICommandHandler<PauseJobCommand, Result>
{
	public ValueTask<Result> Handle(PauseJobCommand c, CancellationToken ct) => ValueTask.FromResult(session.PauseJob());
}
