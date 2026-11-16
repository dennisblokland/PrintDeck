using PrintDeck.Domain.SdCard;
using PrintDeck.Domain.SdCard.Enums;

namespace PrintDeck.Application.Features.SdCard.Dtos;

public sealed record SdCardDto(SdCardState State, IReadOnlyList<SdFile> Files);
