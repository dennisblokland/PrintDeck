using Mediator;

using PrintDeck.Application.Features.Dialogs.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Dialogs.Handlers;

public sealed class AnswerPromptHandler(IPrinterSession session) : ICommandHandler<AnswerPromptCommand, Result>
{
	public ValueTask<Result> Handle(AnswerPromptCommand c, CancellationToken ct) => ValueTask.FromResult(session.AnswerPrompt(c.Choice));
}
