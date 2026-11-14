namespace PrintDeck.Domain.Temperatures;

public sealed record TemperatureSample(DateTimeOffset At, IReadOnlyList<HeaterReading> Heaters);
