using PrintDeck.Domain.Health;

namespace PrintDeck.Application.Features.Health.Dtos;

/// <param name="Pi">Null when the host is not a Raspberry Pi.</param>
/// <param name="Firmware">What the connected printer reports, or null when not connected.</param>
public sealed record HealthReport(DiskSpace? Disk, PiThrottling? Pi, string? Firmware, IReadOnlyList<HealthWarning> Warnings);
