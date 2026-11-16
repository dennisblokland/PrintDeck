using Mediator;

using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.HostControl.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;

namespace PrintDeck.Application.Features.HostControl.Handlers;

public sealed class RunHostActionHandler(
	IHostCommands commands,
	IPrinterSession session,
	ICurrentUser currentUser,
	ILogger<RunHostActionHandler> logger) : ICommandHandler<RunHostActionCommand, Result>
{
	public async ValueTask<Result> Handle(RunHostActionCommand c, CancellationToken ct)
	{
		if (!commands.Available.Contains(c.Action))
			return Result.Failure("This action has no command configured.");
		if (session.State.IsBusy())
			return Result.Failure("Finish or cancel the print first.");

		CurrentUserInfo? user = await currentUser.GetAsync();
		logger.LogWarning("{Action} requested by {User}", c.Action, user?.UserName ?? "unknown");

		// Close the serial port cleanly; a reboot or restart would cut it mid-line otherwise.
		if (session.State.IsConnected() || session.State == PrinterState.Connecting)
			await session.DisconnectAsync();

		return await commands.RunAsync(c.Action, ct);
	}
}
