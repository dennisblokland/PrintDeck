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

public sealed class SetFanHandler(IPrinterSession session) : ICommandHandler<SetFanCommand, Result>
{
	public ValueTask<Result> Handle(SetFanCommand c, CancellationToken ct)
	{
		if (c.Percent is < 0 or > 100) return ValueTask.FromResult(Result.Failure("Fan speed is 0–100 %."));
		string cmd = c.Percent == 0 ? "M107" : $"M106 S{(int) Math.Round(c.Percent * 2.55)}";
		return ValueTask.FromResult(session.Enqueue([cmd]));
	}
}
