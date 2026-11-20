using Mediator;

using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Terminal.Handlers;

public sealed class AddTerminalNoteHandler(IPrinterSession session) : ICommandHandler<AddTerminalNoteCommand, Result>
{
	public const int MaxLength = 500;

	public ValueTask<Result> Handle(AddTerminalNoteCommand c, CancellationToken ct)
	{
		string text = c.Text.ReplaceLineEndings(" ").Trim();
		if (text.Length == 0) return ValueTask.FromResult(Result.Failure("Nothing to write."));
		session.Note(text.Length > MaxLength ? text[..MaxLength] : text);
		return ValueTask.FromResult(Result.Success());
	}
}
