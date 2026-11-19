using System.Globalization;

using PrintDeck.Domain.Settings.Enums;
using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Infrastructure.Timelapse;

/// <summary>The ffmpeg command line that turns numbered frames into an H.264 MP4 that browsers play.</summary>
public static class FfmpegTimelapse
{
	public const string Program = "ffmpeg";

	/// <summary>Frames are named frame-00001.jpg, frame-00002.jpg…</summary>
	public const string FramePattern = "frame-%05d";

	public static string FrameName(int number, string extension) => $"frame-{number.ToString("D5", CultureInfo.InvariantCulture)}{extension}";

	public static IReadOnlyList<string> Arguments(string framesDirectory, string extension, string output, TimelapseRenderOptions options) =>
	[
		"-hide_banner", "-loglevel", "error", "-y",
		"-framerate", options.FrameRate.ToString(CultureInfo.InvariantCulture),
		"-start_number", "1",
		"-i", Path.Combine(framesDirectory, FramePattern + extension),
		"-vf", VideoFilter(options),
		"-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
		"-f", "mp4", output
	];

	/// <summary>
	/// Flips, then rotation (as the dashboard shows the camera), then even width and height, which H.264 in yuv420p needs.
	/// </summary>
	public static string VideoFilter(TimelapseRenderOptions options)
	{
		List<string> filters = [];
		if (options.FlipHorizontal) filters.Add("hflip");
		if (options.FlipVertical) filters.Add("vflip");
		switch (options.Rotation)
		{
			case CameraRotation.Clockwise90: filters.Add("transpose=clock"); break;
			case CameraRotation.Half: filters.Add("hflip,vflip"); break;
			case CameraRotation.Clockwise270: filters.Add("transpose=cclock"); break;
		}
		filters.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
		return string.Join(',', filters);
	}
}
