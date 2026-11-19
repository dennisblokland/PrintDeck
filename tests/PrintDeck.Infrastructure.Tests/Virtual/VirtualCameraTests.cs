using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Virtual;

public class VirtualCameraTests
{
	private sealed class Clock : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
		public override DateTimeOffset GetUtcNow() => Now;
	}

	private readonly Clock _clock = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task<CameraSnapshot> Take()
	{
		Result<CameraSnapshot> r = await new VirtualCamera(_clock).TakeAsync(SnapshotSource.VirtualUri, Ct);
		Assert.True(r.IsSuccess);
		return r.Value!;
	}

	/// <summary>Bit by bit, as the PNG specification writes it; independent of the table the encoder uses.</summary>
	private static uint Crc32(ReadOnlySpan<byte> bytes)
	{
		uint crc = 0xFFFFFFFFu;
		foreach (byte b in bytes)
		{
			crc ^= b;
			for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
		}
		return crc ^ 0xFFFFFFFFu;
	}

	/// <summary>Checks every chunk's CRC and returns the size and the unpacked pixel rows.</summary>
	private static (int Width, int Height, byte[] Pixels) ReadPng(byte[] png)
	{
		Assert.Equal([0x89, (byte) 'P', (byte) 'N', (byte) 'G', 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
		int width = 0, height = 0;
		using MemoryStream idat = new();
		int at = 8;
		while (at < png.Length)
		{
			int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
			string type = Encoding.ASCII.GetString(png, at + 4, 4);
			byte[] data = png[(at + 8)..(at + 8 + length)];
			uint crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at + 8 + length));
			Assert.Equal(Crc32(png.AsSpan(at + 4, 4 + length)), crc);
			if (type == "IHDR")
			{
				width = BinaryPrimitives.ReadInt32BigEndian(data);
				height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
				Assert.Equal((8, 2), (data[8], data[9]));
			}
			else if (type == "IDAT") idat.Write(data);
			at += 12 + length;
		}
		idat.Position = 0;
		using ZLibStream zlib = new(idat, CompressionMode.Decompress);
		using MemoryStream pixels = new();
		zlib.CopyTo(pixels);
		return (width, height, pixels.ToArray());
	}

	[Fact]
	public async Task Takes_a_valid_png()
	{
		CameraSnapshot shot = await Take();

		Assert.Equal("image/png", shot.ContentType);
		(int width, int height, byte[] pixels) = ReadPng(shot.Content);
		Assert.Equal((VirtualCamera.Width, VirtualCamera.Height), (width, height));
		Assert.Equal(height * (1 + width * 3), pixels.Length);
	}

	[Fact]
	public async Task The_print_grows_over_time()
	{
		byte[] first = (await Take()).Content;
		_clock.Now = _clock.Now.AddSeconds(30);
		byte[] later = (await Take()).Content;

		Assert.NotEqual(first, later);
	}
}
