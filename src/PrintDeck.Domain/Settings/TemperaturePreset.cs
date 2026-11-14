namespace PrintDeck.Domain.Settings;

public sealed record TemperaturePreset(string Name, int Hotend, int Bed);
