using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class DeletePrinterProfileHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<DeletePrinterProfileCommand, Result>
{
	public async ValueTask<Result> Handle(DeletePrinterProfileCommand c, CancellationToken ct)
	{
		string? error = null;
		await settings.UpdateAsync(s =>
		{
			error = s.FindProfile(c.Id) switch
			{
				null => "That profile no longer exists.",
				{ } p when p.Id == s.ActiveProfile.Id => "Switch to another profile before deleting this one.",
				_ => null
			};
			return error is null ? s with { Profiles = s.Profiles.Where(p => p.Id != c.Id).ToList() } : s;
		}, ct);

		if (error is not null) return Result.Failure(error);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
