using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

using PrintDeck.Application.Files.Thumbnails;

namespace PrintDeck.Application.Tests.Files.Thumbnails;

public class PngEncoderTests
{
	[Fact]
	public void Writes_a_valid_rgba_png()
	{
		RgbaImage image = new(2, 2, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);

		byte[] png = PngEncoder.Encode(image);

		Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
		List<(string Type, byte[] Data)> chunks = Chunks(png);
		Assert.Equal(["IHDR", "IDAT", "IEND"], chunks.Select(c => c.Type));

		byte[] ihdr = chunks[0].Data;
		Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(ihdr));
		Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(ihdr.AsSpan(4)));
		Assert.Equal((8, 6), (ihdr[8], ihdr[9]));

		using MemoryStream raw = new();
		using (ZLibStream zlib = new(new MemoryStream(chunks[1].Data), CompressionMode.Decompress))
			zlib.CopyTo(raw);
		Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 9, 10, 11, 12, 13, 14, 15, 16 }, raw.ToArray());
	}

	/// <summary>Splits a PNG into chunks and checks each CRC.</summary>
	private static List<(string, byte[])> Chunks(byte[] png)
	{
		List<(string, byte[])> chunks = [];
		int p = 8;
		while (p < png.Length)
		{
			int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p));
			string type = Encoding.ASCII.GetString(png, p + 4, 4);
			byte[] data = png[(p + 8)..(p + 8 + length)];
			uint crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(p + 8 + length));
			Assert.Equal(PngEncoder.Crc(0xFFFFFFFF, png.AsSpan(p + 4, 4 + length)) ^ 0xFFFFFFFF, crc);
			chunks.Add((type, data));
			p += 12 + length;
		}
		return chunks;
	}
}
