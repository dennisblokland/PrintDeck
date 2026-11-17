using Mediator;

using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Control.Handlers;

public sealed class SetFeedRateHandler(IPrinterSession session) : ICommandHandler<SetFeedRateCommand, Result>
{
	public const int Min = 50, Max = 200;

	public ValueTask<Result> Handle(SetFeedRateCommand c, CancellationToken ct) =>
		ValueTask.FromResult(c.Percent is < Min or > Max
			? Result.Failure($"Feed rate is {Min}–{Max} %.")
			: session.Enqueue([$"M220 S{c.Percent}"]));
}
