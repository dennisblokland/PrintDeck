using Mediator;

using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Application.Features.Terminal.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Features.Terminal.Handlers;

public sealed class ClearTerminalHandler(IPrinterSession session) : ICommandHandler<ClearTerminalCommand, Result>
{
	public ValueTask<Result> Handle(ClearTerminalCommand c, CancellationToken ct)
	{
		session.ClearTerminal();
		return ValueTask.FromResult(Result.Success());
	}
}
