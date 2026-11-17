using PrintDeck.Application.Files.Thumbnails;

namespace PrintDeck.Application.Tests.Files.Thumbnails;

public class QoiDecoderTests
{
	private static readonly byte[] _end = [0, 0, 0, 0, 0, 0, 0, 1];

	private static byte[] File(int width, int height, params byte[] chunks) =>
		[.. "qoif"u8.ToArray(), 0, 0, 0, (byte)width, 0, 0, 0, (byte)height, 4, 0, .. chunks, .. _end];

	[Fact]
	public void Decodes_every_kind_of_chunk()
	{
		byte[] qoi = File(5, 1,
			0xFE, 10, 20, 30,   // RGB: (10, 20, 30)
			0x76,               // DIFF: r+1, g-1, b+0
			0xA5, 0x96,         // LUMA: g+5, r-g+1, b-g-2
			0x09,               // INDEX: slot of (10, 20, 30, 255)
			0xFF, 1, 2, 3, 128  // RGBA
		);

		RgbaImage? image = QoiDecoder.TryDecode(qoi);

		Assert.NotNull(image);
		Assert.Equal((5, 1), (image.Width, image.Height));
		Assert.Equal(new byte[]
		{
			10, 20, 30, 255,
			11, 19, 30, 255,
			17, 24, 33, 255,
			10, 20, 30, 255,
			1, 2, 3, 128
		}, image.Pixels);
	}

	[Fact]
	public void Repeats_a_run()
	{
		RgbaImage? image = QoiDecoder.TryDecode(File(3, 1, 0xFE, 9, 8, 7, 0xC1));

		Assert.Equal(new byte[] { 9, 8, 7, 255, 9, 8, 7, 255, 9, 8, 7, 255 }, image?.Pixels);
	}

	[Fact]
	public void Rejects_other_data()
	{
		Assert.Null(QoiDecoder.TryDecode("not a qoi file at all, sorry"u8));
		Assert.Null(QoiDecoder.TryDecode(File(0, 1)));
		Assert.Null(QoiDecoder.TryDecode(File(4, 1, 0xFE, 1, 2, 3))); // three pixels missing
	}
}
