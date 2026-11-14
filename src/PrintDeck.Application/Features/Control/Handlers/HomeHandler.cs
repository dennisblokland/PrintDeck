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

public sealed class HomeHandler(IPrinterSession session) : ICommandHandler<HomeCommand, Result>
{
	public ValueTask<Result> Handle(HomeCommand c, CancellationToken ct)
	{
		if (Guard.CanMove(session) is { IsSuccess: false } blocked) return ValueTask.FromResult(blocked);
		string axes = c.Axes == Axis.All || c.Axes == Axis.None
			? ""
			: string.Join(' ', new[] { Axis.X, Axis.Y, Axis.Z }.Where(a => c.Axes.HasFlag(a)));
		return ValueTask.FromResult(session.Enqueue([$"G28 {axes}".Trim()]));
	}
}
