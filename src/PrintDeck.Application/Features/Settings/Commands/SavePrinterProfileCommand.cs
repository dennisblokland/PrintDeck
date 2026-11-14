using Mediator;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Commands;

/// <summary>
/// Saves a printer profile. An empty <see cref="PrinterProfile.Id"/> adds a new profile; the result is its id.
/// </summary>
public sealed record SavePrinterProfileCommand(PrinterProfile Profile) : ICommand<Result<string>>;
