using Mediator;

using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Queries;

/// <summary>The active printer profile.</summary>
public sealed record GetPrinterProfileQuery : IQuery<PrinterProfile>;
