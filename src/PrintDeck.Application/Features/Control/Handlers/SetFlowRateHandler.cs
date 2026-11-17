using Mediator;

using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Control.Handlers;

public sealed class SetFlowRateHandler(IPrinterSession session) : ICommandHandler<SetFlowRateCommand, Result>
{
	public const int Min = 75, Max = 125;

	public ValueTask<Result> Handle(SetFlowRateCommand c, CancellationToken ct) =>
		ValueTask.FromResult(c.Percent is < Min or > Max
			? Result.Failure($"Flow is {Min}–{Max} %.")
			: session.Enqueue([$"M221 S{c.Percent}"]));
}
