using Mediator;

using PrintDeck.Application.Features.Health.Dtos;

namespace PrintDeck.Application.Features.Health.Queries;

/// <summary>Disk space, Raspberry Pi power and known-bad firmware, with a warning for each problem found.</summary>
public sealed record GetHealthQuery : IQuery<HealthReport>;
