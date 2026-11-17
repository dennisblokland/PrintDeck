using System.Text;

using PrintDeck.Application.Files.Thumbnails;

namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>Small images in the formats slicers embed, and the G-code comment blocks that carry them.</summary>
internal static class ThumbnailSamples
{
	public static RgbaImage Image(int width, int height)
	{
		byte[] pixels = new byte[width * height * 4];
		for (int i = 0; i < pixels.Length; i += 4)
		{
			pixels[i] = (byte)(i * 7);
			pixels[i + 1] = (byte)(i * 3);
			pixels[i + 2] = 200;
			pixels[i + 3] = 255;
		}
		return new RgbaImage(width, height, pixels);
	}

	public static byte[] Png(int width, int height) => PngEncoder.Encode(Image(width, height));

	/// <summary>A QOI file that stores every pixel with QOI_OP_RGBA.</summary>
	public static byte[] Qoi(RgbaImage image)
	{
		List<byte> bytes = [.. "qoif"u8.ToArray(), .. BigEndian(image.Width), .. BigEndian(image.Height), 4, 0];
		for (int i = 0; i < image.Pixels.Length; i += 4)
			bytes.AddRange([0xFF, image.Pixels[i], image.Pixels[i + 1], image.Pixels[i + 2], image.Pixels[i + 3]]);
		bytes.AddRange([0, 0, 0, 0, 0, 0, 0, 1]);
		return [.. bytes];
	}

	/// <summary>Just enough of a JPEG to read its size: start of image, a start-of-frame segment, end of image.</summary>
	public static byte[] Jpeg(int width, int height) =>
	[
		0xFF, 0xD8,
		0xFF, 0xE0, 0x00, 0x04, 0x4A, 0x46,
		0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x03,
		0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
		0xFF, 0xD9
	];

	/// <summary>The comment block a slicer writes, base64 wrapped at 78 characters.</summary>
	public static string Block(string keyword, int width, int height, byte[] data)
	{
		string base64 = Convert.ToBase64String(data);
		StringBuilder sb = new();
		sb.Append($"; {keyword} begin {width}x{height} {base64.Length}\n");
		for (int i = 0; i < base64.Length; i += 78)
			sb.Append("; ").Append(base64.AsSpan(i, Math.Min(78, base64.Length - i))).Append('\n');
		sb.Append($"; {keyword} end\n;\n");
		return sb.ToString();
	}

	private static byte[] BigEndian(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
}
