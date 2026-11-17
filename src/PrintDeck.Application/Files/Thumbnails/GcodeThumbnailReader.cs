using System.Text;
using System.Text.RegularExpressions;

using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Files.Thumbnails;

/// <summary>
/// Finds the preview images PrusaSlicer, OrcaSlicer and Cura embed at the top of a G-code file:
/// <code>
/// ; thumbnail begin 160x120 3276        (PNG; also thumbnail_PNG, thumbnail_JPG, thumbnail_QOI)
/// ; iVBORw0KGgoAAAANSUhEUgAAAKAAAAB4...
/// ; thumbnail end
/// </code>
/// Returns the largest one, preferring PNG or JPEG over QOI at the same size. QOI, which browsers cannot show, becomes PNG.
/// </summary>
public static partial class GcodeThumbnailReader
{
	/// <summary>Thumbnails sit in the header; stop after this many G-code commands.</summary>
	private const int MaxCommandsToScan = 20;

	/// <summary>Ignore blocks larger than this (base64 characters), so a broken file cannot use up memory.</summary>
	private const int MaxBlockChars = 4 * 1024 * 1024;

	[GeneratedRegex(@"^thumbnail(?:_(PNG|JPG|QOI))?\s+begin\s+(\d+)\s*x\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex BlockStart();

	[GeneratedRegex(@"^thumbnail(?:_(?:PNG|JPG|QOI))?\s+end\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex BlockEnd();

	public static async Task<GcodeThumbnail?> ReadAsync(Stream stream, CancellationToken ct)
	{
		using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
		GcodeThumbnail? best = null;
		bool bestIsQoi = false;
		StringBuilder? block = null;
		string format = "";
		int commands = 0;

		while (await reader.ReadLineAsync(ct) is { } raw)
		{
			string line = raw.Trim();
			if (line.Length == 0) continue;

			if (line[0] != ';')
			{
				block = null; // a block never spans G-code
				if (++commands >= MaxCommandsToScan) break;
				continue;
			}

			string c = line.TrimStart(';').Trim();
			if (block is null)
			{
				if (BlockStart().Match(c) is { Success: true } m)
				{
					block = new StringBuilder();
					format = m.Groups[1].Success ? m.Groups[1].Value.ToUpperInvariant() : "PNG";
				}
				continue;
			}

			if (!BlockEnd().IsMatch(c))
			{
				if (block.Length + c.Length > MaxBlockChars) block = null;
				else block.Append(c);
				continue;
			}

			if (Decode(format, block.ToString()) is { } thumbnail)
			{
				bool isQoi = format == "QOI";
				long area = (long)thumbnail.Width * thumbnail.Height;
				long bestArea = best is null ? -1 : (long)best.Width * best.Height;
				if (area > bestArea || (area == bestArea && bestIsQoi && !isQoi))
				{
					best = thumbnail;
					bestIsQoi = isQoi;
				}
			}
			block = null;
		}
		return best;
	}

	private static GcodeThumbnail? Decode(string format, string base64)
	{
		byte[] data;
		try { data = Convert.FromBase64String(base64); }
		catch (FormatException) { return null; }

		return format switch
		{
			"QOI" => QoiDecoder.TryDecode(data) is { } image ? new GcodeThumbnail(image.Width, image.Height, GcodeThumbnail.Png, PngEncoder.Encode(image)) : null,
			"JPG" => IsJpeg(data) && JpegSize(data) is { } jpg ? new GcodeThumbnail(jpg.Width, jpg.Height, GcodeThumbnail.Jpeg, data) : null,
			_ => PngSize(data) is { } png ? new GcodeThumbnail(png.Width, png.Height, GcodeThumbnail.Png, data) : null
		};
	}

	private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

	/// <summary>Width and height from the IHDR chunk, which always comes first.</summary>
	private static (int Width, int Height)? PngSize(byte[] d)
	{
		if (d.Length < 24 || !d.AsSpan(0, 8).SequenceEqual(_pngSignature) || Encoding.ASCII.GetString(d, 12, 4) != "IHDR") return null;
		int w = BigEndian(d, 16), h = BigEndian(d, 20);
		return w > 0 && h > 0 ? (w, h) : null;
	}

	private static bool IsJpeg(byte[] d) => d.Length > 4 && d[0] == 0xFF && d[1] == 0xD8;

	/// <summary>Width and height from the first start-of-frame marker.</summary>
	private static (int Width, int Height)? JpegSize(byte[] d)
	{
		int i = 2;
		while (i + 9 < d.Length)
		{
			if (d[i] != 0xFF) { i++; continue; }
			byte marker = d[i + 1];
			if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
			{
				int h = (d[i + 5] << 8) | d[i + 6], w = (d[i + 7] << 8) | d[i + 8];
				return w > 0 && h > 0 ? (w, h) : null;
			}
			if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { i += 2; continue; }
			i += 2 + ((d[i + 2] << 8) | d[i + 3]);
		}
		return null;
	}

	private static int BigEndian(byte[] d, int at) => (d[at] << 24) | (d[at + 1] << 16) | (d[at + 2] << 8) | d[at + 3];
}
