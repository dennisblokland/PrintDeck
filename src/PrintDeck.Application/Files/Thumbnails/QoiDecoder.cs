namespace PrintDeck.Application.Files.Thumbnails;

/// <summary>Decoder for the "Quite OK Image" format (qoiformat.org), which PrusaSlicer embeds for Prusa printers.</summary>
public static class QoiDecoder
{
	private const int HeaderSize = 14;
	private const int MaxPixels = 4096 * 4096;

	public static RgbaImage? TryDecode(ReadOnlySpan<byte> data)
	{
		if (data.Length < HeaderSize + 8 || data[0] != (byte)'q' || data[1] != (byte)'o' || data[2] != (byte)'i' || data[3] != (byte)'f')
			return null;

		uint width = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
		uint height = (uint)((data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11]);
		if (width == 0 || height == 0 || (ulong)width * height > MaxPixels) return null;

		int count = (int)(width * height);
		byte[] pixels = new byte[count * 4];
		Span<byte> seen = stackalloc byte[64 * 4];
		seen.Clear();
		byte r = 0, g = 0, b = 0, a = 255;
		int run = 0;
		int p = HeaderSize;
		int end = data.Length - 8; // 7 zero bytes and a 1 close the stream

		for (int px = 0; px < count; px++)
		{
			if (run > 0)
			{
				run--;
			}
			else
			{
				if (p >= end) return null;
				byte op = data[p++];
				if (op == 0xFE)
				{
					if (p + 3 > end) return null;
					r = data[p++]; g = data[p++]; b = data[p++];
				}
				else if (op == 0xFF)
				{
					if (p + 4 > end) return null;
					r = data[p++]; g = data[p++]; b = data[p++]; a = data[p++];
				}
				else switch (op >> 6)
				{
					case 0: // index
						int i = (op & 0x3F) * 4;
						r = seen[i]; g = seen[i + 1]; b = seen[i + 2]; a = seen[i + 3];
						break;
					case 1: // small difference
						r = (byte)(r + ((op >> 4) & 3) - 2);
						g = (byte)(g + ((op >> 2) & 3) - 2);
						b = (byte)(b + (op & 3) - 2);
						break;
					case 2: // luma difference
						if (p >= end) return null;
						byte next = data[p++];
						int dg = (op & 0x3F) - 32;
						r = (byte)(r + dg - 8 + ((next >> 4) & 0x0F));
						g = (byte)(g + dg);
						b = (byte)(b + dg - 8 + (next & 0x0F));
						break;
					default: // run
						run = op & 0x3F;
						break;
				}

				int slot = (r * 3 + g * 5 + b * 7 + a * 11) % 64 * 4;
				seen[slot] = r; seen[slot + 1] = g; seen[slot + 2] = b; seen[slot + 3] = a;
			}

			int o = px * 4;
			pixels[o] = r; pixels[o + 1] = g; pixels[o + 2] = b; pixels[o + 3] = a;
		}

		return new RgbaImage((int)width, (int)height, pixels);
	}
}
