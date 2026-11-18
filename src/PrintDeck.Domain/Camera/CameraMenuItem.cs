namespace PrintDeck.Domain.Camera;

/// <summary>One choice of a menu control, e.g. 1 "Manual Mode" for auto_exposure.</summary>
public sealed record CameraMenuItem(long Value, string Label);
