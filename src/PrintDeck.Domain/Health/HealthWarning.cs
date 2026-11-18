using PrintDeck.Domain.Health.Enums;

namespace PrintDeck.Domain.Health;

/// <param name="Id">Stable per kind of problem, so a dismissed warning stays dismissed while it lasts.</param>
public sealed record HealthWarning(string Id, HealthSeverity Severity, string Title, string Detail);
