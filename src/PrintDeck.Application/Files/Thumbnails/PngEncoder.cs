using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PrintDeck.Application.Files.Thumbnails;

/// <summary>Writes an <see cref="RgbaImage"/> as an 8-bit RGBA PNG, without row filters.</summary>
public static class PngEncoder
{
	private static readonly byte[] _signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
	private static readonly uint[] _crcTable = BuildCrcTable();

	public static byte[] Encode(RgbaImage image)
	{
		int stride = image.Width * 4;
		byte[] raw = new byte[(stride + 1) * image.Height];
		for (int y = 0; y < image.Height; y++)
			Buffer.BlockCopy(image.Pixels, y * stride, raw, y * (stride + 1) + 1, stride); // filter byte 0 = none

		using MemoryStream compressed = new();
		using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
			zlib.Write(raw);

		byte[] header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
		header[8] = 8; // bits per channel
		header[9] = 6; // RGBA

		using MemoryStream png = new();
		png.Write(_signature);
		WriteChunk(png, "IHDR", header);
		WriteChunk(png, "IDAT", compressed.ToArray());
		WriteChunk(png, "IEND", []);
		return png.ToArray();
	}

	private static void WriteChunk(Stream s, string type, byte[] data)
	{
		Span<byte> four = stackalloc byte[4];
		BinaryPrimitives.WriteInt32BigEndian(four, data.Length);
		s.Write(four);

		byte[] typeBytes = Encoding.ASCII.GetBytes(type);
		s.Write(typeBytes);
		s.Write(data);

		uint crc = Crc(Crc(0xFFFFFFFF, typeBytes), data) ^ 0xFFFFFFFF;
		BinaryPrimitives.WriteUInt32BigEndian(four, crc);
		s.Write(four);
	}

	internal static uint Crc(uint crc, ReadOnlySpan<byte> data)
	{
		foreach (byte b in data) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
		return crc;
	}

	private static uint[] BuildCrcTable()
	{
		uint[] table = new uint[256];
		for (uint n = 0; n < 256; n++)
		{
			uint c = n;
			for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
			table[n] = c;
		}
		return table;
	}
}
