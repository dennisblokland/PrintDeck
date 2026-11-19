using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Application.Features.Timelapse.Dtos;

/// <summary>The rendered timelapses, newest first, and what the recorder is doing.</summary>
public sealed record TimelapsesDto(TimelapseStatus Status, IReadOnlyList<TimelapseVideo> Videos);
