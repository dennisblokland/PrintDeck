using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Abstractions.Interfaces;

public interface ISettingsStore
{
	Task<PrinterProfile> GetAsync(CancellationToken ct);
	Task SaveAsync(PrinterProfile profile, CancellationToken ct);
}
