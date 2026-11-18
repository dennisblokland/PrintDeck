namespace PrintDeck.Domain.Camera;

/// <summary>One still image from the camera, as the camera sent it (not rotated or flipped).</summary>
public sealed record CameraSnapshot(byte[] Content, string ContentType);
