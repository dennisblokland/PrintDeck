using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class SelectPrinterProfileHandler(IPrinterSession session, ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<SelectPrinterProfileCommand, Result>
{
	public async ValueTask<Result> Handle(SelectPrinterProfileCommand c, CancellationToken ct)
	{
		HostSettings current = await settings.GetAsync(ct);
		if (current.ActiveProfile.Id == c.Id) return Result.Success();
		if (session.State.IsBusy()) return Result.Failure("Finish or cancel the print before switching printer profiles.");

		bool found = false;
		await settings.UpdateAsync(s =>
		{
			found = s.FindProfile(c.Id) is not null;
			return found ? s with { ActiveProfileId = c.Id } : s;
		}, ct);

		if (!found) return Result.Failure("That profile no longer exists.");
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
