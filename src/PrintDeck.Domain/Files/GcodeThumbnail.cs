namespace PrintDeck.Domain.Files;

/// <summary>A preview image a slicer embedded in a G-code file, ready to serve (PNG or JPEG).</summary>
public sealed record GcodeThumbnail(int Width, int Height, string ContentType, byte[] Data)
{
	public const string Png = "image/png";
	public const string Jpeg = "image/jpeg";
}
