using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Control.Handlers;

public sealed class RunCustomButtonHandler(IPrinterSession session, ISettingsStore settings) : ICommandHandler<RunCustomButtonCommand, Result>
{
	public const string Changed = "The buttons have changed; reload the page.";

	public async ValueTask<Result> Handle(RunCustomButtonCommand c, CancellationToken ct)
	{
		HostSettings s = await settings.GetAsync(ct);
		if (c.Index < 0 || c.Index >= s.CustomButtons.Count || s.CustomButtons[c.Index].Label != c.Label) return Result.Failure(Changed);
		CustomButton button = s.CustomButtons[c.Index];

		// Checked here too, not only by the disabled button: a page can be out of date.
		if (button.IdleOnly && Guard.CanMove(session) is { IsSuccess: false } refused) return refused;
		if (!session.State.IsConnected()) return Result.Failure("Printer is not connected.");

		Result<IReadOnlyList<string>> commands = button.Commands(s.Snippets);
		return commands.IsSuccess ? session.Enqueue(commands.Value!) : Result.Failure(commands.Error!);
	}
}
