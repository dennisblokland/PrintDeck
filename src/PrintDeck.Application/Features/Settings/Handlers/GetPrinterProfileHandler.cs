using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class GetPrinterProfileHandler(ISettingsStore settings) : IQueryHandler<GetPrinterProfileQuery, PrinterProfile>
{
	public async ValueTask<PrinterProfile> Handle(GetPrinterProfileQuery q, CancellationToken ct) =>
		(await settings.GetAsync(ct)).ActiveProfile;
}
