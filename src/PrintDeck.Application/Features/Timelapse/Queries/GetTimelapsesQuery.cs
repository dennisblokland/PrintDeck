using Mediator;

using PrintDeck.Application.Features.Timelapse.Dtos;

namespace PrintDeck.Application.Features.Timelapse.Queries;

public sealed record GetTimelapsesQuery : IQuery<TimelapsesDto>;
