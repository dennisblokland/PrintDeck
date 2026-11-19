using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Infrastructure.Camera;

namespace PrintDeck.Infrastructure.Virtual;

/// <summary>
/// A pretend webcam for the <c>VIRTUAL</c> snapshot URL: a print that grows on the bed over each minute while the
/// nozzle sweeps across it, so snapshots and timelapses can be tried without a camera.
/// </summary>
public sealed class VirtualCamera(TimeProvider time) : ICameraSnapshots
{
	public const int Width = 320;
	public const int Height = 240;

	private static readonly (byte R, byte G, byte B) _background = (0x1E, 0x23, 0x2B);
	private static readonly (byte R, byte G, byte B) _bed = (0x6B, 0x72, 0x80);
	private static readonly (byte R, byte G, byte B) _print = (0xF2, 0x8C, 0x28);
	private static readonly (byte R, byte G, byte B) _nozzle = (0xE5, 0xE7, 0xEB);

	public Task<Result<CameraSnapshot>> TakeAsync(Uri url, CancellationToken ct)
	{
		DateTimeOffset now = time.GetUtcNow();
		double minute = (now.Second + now.Millisecond / 1000.0) / 60.0;
		byte[] rgb = new byte[Width * Height * 3];

		Fill(rgb, 0, 0, Width, Height, _background);
		const int bedTop = Height - 30;
		Fill(rgb, 20, bedTop, Width - 20, bedTop + 8, _bed);

		const int printLeft = 110, printRight = 210, maxHeight = 150;
		int printTop = bedTop - (int) Math.Round(maxHeight * minute);
		Fill(rgb, printLeft, printTop, printRight, bedTop, _print);

		// The nozzle sweeps left and right a few times per layer, just above the print.
		double sweep = Math.Abs(((minute * 12) % 2) - 1);
		int nozzleX = printLeft + (int) Math.Round((printRight - printLeft - 12) * sweep);
		Fill(rgb, nozzleX, printTop - 22, nozzleX + 12, printTop - 2, _nozzle);
		Fill(rgb, 0, printTop - 30, Width, printTop - 26, _nozzle); // gantry

		return Task.FromResult(Result.Success(new CameraSnapshot(PngImage.Encode(Width, Height, rgb), "image/png")));
	}

	private static void Fill(byte[] rgb, int left, int top, int right, int bottom, (byte R, byte G, byte B) color)
	{
		left = Math.Clamp(left, 0, Width);
		right = Math.Clamp(right, 0, Width);
		top = Math.Clamp(top, 0, Height);
		bottom = Math.Clamp(bottom, 0, Height);
		for (int y = top; y < bottom; y++)
		for (int x = left; x < right; x++)
		{
			int i = (y * Width + x) * 3;
			rgb[i] = color.R;
			rgb[i + 1] = color.G;
			rgb[i + 2] = color.B;
		}
	}
}
