using PrintDeck.Domain.Settings.Enums;
using PrintDeck.Domain.Timelapse;
using PrintDeck.Infrastructure.Timelapse;

namespace PrintDeck.Infrastructure.Tests.Timelapse;

public class FfmpegTimelapseTests
{
	[Theory]
	[InlineData(CameraRotation.None, false, false, "scale=trunc(iw/2)*2:trunc(ih/2)*2")]
	[InlineData(CameraRotation.Clockwise90, false, false, "transpose=clock,scale=trunc(iw/2)*2:trunc(ih/2)*2")]
	[InlineData(CameraRotation.Half, false, false, "hflip,vflip,scale=trunc(iw/2)*2:trunc(ih/2)*2")]
	[InlineData(CameraRotation.Clockwise270, false, false, "transpose=cclock,scale=trunc(iw/2)*2:trunc(ih/2)*2")]
	[InlineData(CameraRotation.Clockwise90, true, true, "hflip,vflip,transpose=clock,scale=trunc(iw/2)*2:trunc(ih/2)*2")]
	public void Flips_then_turns_the_picture_like_the_dashboard(CameraRotation rotation, bool flipH, bool flipV, string filter)
	{
		Assert.Equal(filter, FfmpegTimelapse.VideoFilter(new TimelapseRenderOptions(25, rotation, flipH, flipV)));
	}

	[Fact]
	public void Renders_numbered_frames_to_an_mp4_browsers_play()
	{
		IReadOnlyList<string> args = FfmpegTimelapse.Arguments("/data/timelapse/.frames/abc", ".jpg", "/out/video.mp4", new TimelapseRenderOptions(30));

		Assert.Equal("30", args[args.ToList().IndexOf("-framerate") + 1]);
		Assert.Equal(Path.Combine("/data/timelapse/.frames/abc", "frame-%05d.jpg"), args[args.ToList().IndexOf("-i") + 1]);
		Assert.Equal(["-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-f", "mp4", "/out/video.mp4"], args.TakeLast(9));
	}

	[Fact]
	public void Frame_names_match_the_pattern()
	{
		Assert.Equal("frame-00001.jpg", FfmpegTimelapse.FrameName(1, ".jpg"));
		Assert.Equal("frame-12345.png", FfmpegTimelapse.FrameName(12345, ".png"));
	}
}
