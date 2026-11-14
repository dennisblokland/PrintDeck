using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Jobs.Handlers;

public sealed class ResumeJobHandler(IPrinterSession session) : ICommandHandler<ResumeJobCommand, Result>
{
	public ValueTask<Result> Handle(ResumeJobCommand c, CancellationToken ct) => ValueTask.FromResult(session.ResumeJob());
}
