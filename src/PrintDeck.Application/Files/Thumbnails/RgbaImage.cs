namespace PrintDeck.Application.Files.Thumbnails;

/// <summary>Decoded pixels, 4 bytes per pixel (red, green, blue, alpha), row by row from the top.</summary>
public sealed record RgbaImage(int Width, int Height, byte[] Pixels);
