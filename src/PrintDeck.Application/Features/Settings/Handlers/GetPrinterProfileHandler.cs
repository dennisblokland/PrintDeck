using System.Security.Cryptography;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class GetPrinterProfileHandler(ISettingsStore settings) : IQueryHandler<GetPrinterProfileQuery, PrinterProfile>
{
	public ValueTask<PrinterProfile> Handle(GetPrinterProfileQuery q, CancellationToken ct) => new(settings.GetAsync(ct));
}
