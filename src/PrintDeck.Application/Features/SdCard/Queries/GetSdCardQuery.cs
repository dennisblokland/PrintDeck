using Mediator;

using PrintDeck.Application.Features.SdCard.Dtos;

namespace PrintDeck.Application.Features.SdCard.Queries;

public sealed record GetSdCardQuery : IQuery<SdCardDto>;
