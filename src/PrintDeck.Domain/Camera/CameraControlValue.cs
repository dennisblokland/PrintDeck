namespace PrintDeck.Domain.Camera;

/// <summary>A control value chosen in PrintDeck, kept in the settings and applied again when PrintDeck starts.</summary>
public sealed record CameraControlValue(string Name, long Value);
