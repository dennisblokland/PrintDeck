using Mediator;

using PrintDeck.Application.Features.Dialogs.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Dialogs.Handlers;

public sealed class DismissNotificationHandler(IPrinterSession session) : ICommandHandler<DismissNotificationCommand, Result>
{
	public ValueTask<Result> Handle(DismissNotificationCommand c, CancellationToken ct)
	{
		session.DismissNotification();
		return ValueTask.FromResult(Result.Success());
	}
}
