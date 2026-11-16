using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Dtos;

/// <param name="Recent">Newest first, at most the number asked for.</param>
/// <param name="Totals">Over every recorded print, not only <paramref name="Recent"/>.</param>
public sealed record PrintHistoryDto(IReadOnlyList<PrintHistoryEntry> Recent, PrintStats Totals);
