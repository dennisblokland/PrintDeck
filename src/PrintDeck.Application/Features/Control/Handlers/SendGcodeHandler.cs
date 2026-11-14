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

public sealed class SendGcodeHandler(IPrinterSession session) : ICommandHandler<SendGcodeCommand, Result>
{
	public ValueTask<Result> Handle(SendGcodeCommand c, CancellationToken ct)
	{
		string[] lines = c.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return ValueTask.FromResult(lines.Length == 0 ? Result.Failure("Nothing to send.") : session.Enqueue(lines));
	}
}
