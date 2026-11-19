using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PrintDeck.Infrastructure.Camera;

/// <summary>Writes an 8-bit RGB picture as a PNG; enough for the virtual camera, no image library needed.</summary>
public static class PngImage
{
	private static readonly byte[] _signature = [0x89, (byte) 'P', (byte) 'N', (byte) 'G', 0x0D, 0x0A, 0x1A, 0x0A];
	private static readonly uint[] _crcTable = BuildCrcTable();

	/// <param name="rgb">Width × height pixels, three bytes each, row by row from the top.</param>
	public static byte[] Encode(int width, int height, ReadOnlySpan<byte> rgb)
	{
		if (width <= 0 || height <= 0 || rgb.Length != width * height * 3)
			throw new ArgumentException("The pixels don't match the size.", nameof(rgb));

		using MemoryStream png = new();
		png.Write(_signature);

		byte[] header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(header, width);
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
		header[8] = 8; // bits per channel
		header[9] = 2; // RGB
		WriteChunk(png, "IHDR", header);

		using MemoryStream data = new();
		using (ZLibStream zlib = new(data, CompressionLevel.Fastest, leaveOpen: true))
		{
			int stride = width * 3;
			for (int y = 0; y < height; y++)
			{
				zlib.WriteByte(0); // no filter
				zlib.Write(rgb.Slice(y * stride, stride));
			}
		}
		WriteChunk(png, "IDAT", data.ToArray());
		WriteChunk(png, "IEND", []);
		return png.ToArray();
	}

	private static void WriteChunk(Stream png, string type, byte[] content)
	{
		Span<byte> number = stackalloc byte[4];
		BinaryPrimitives.WriteInt32BigEndian(number, content.Length);
		png.Write(number);
		byte[] typeBytes = Encoding.ASCII.GetBytes(type);
		png.Write(typeBytes);
		png.Write(content);
		uint crc = Crc(Crc(0xFFFFFFFFu, typeBytes), content) ^ 0xFFFFFFFFu;
		BinaryPrimitives.WriteUInt32BigEndian(number, crc);
		png.Write(number);
	}

	private static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
	{
		foreach (byte b in bytes) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
		return crc;
	}

	private static uint[] BuildCrcTable()
	{
		uint[] table = new uint[256];
		for (uint n = 0; n < 256; n++)
		{
			uint c = n;
			for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
			table[n] = c;
		}
		return table;
	}
}
