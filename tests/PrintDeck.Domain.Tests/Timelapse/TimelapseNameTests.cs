using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Domain.Tests.Timelapse;

public class TimelapseNameTests
{
	private static readonly DateTimeOffset _started = new(2026, 11, 19, 14, 30, 5, TimeSpan.Zero);
	private static string Stamp => _started.ToLocalTime().ToString("yyyyMMdd-HHmmss");

	[Fact]
	public void Names_the_video_after_the_file_and_the_start()
	{
		Assert.Equal($"benchy_{Stamp}.mp4", TimelapseName.For("benchy.gcode", _started));
	}

	[Fact]
	public void Leaves_out_the_folder_and_replaces_awkward_characters()
	{
		Assert.Equal($"Big_box_v2.0_{Stamp}.mp4", TimelapseName.For("parts/Big box (v2.0).gcode", _started));
		Assert.Equal($"print_{Stamp}.mp4", TimelapseName.For("((( ))).gcode", _started));
	}

	[Theory]
	[InlineData("benchy_20261119-143005.mp4", true)]
	[InlineData("BENCHY.MP4", true)]
	[InlineData(".mp4", false)]
	[InlineData(".hidden.mp4", false)]
	[InlineData("../settings.mp4", false)]
	[InlineData("a\\b.mp4", false)]
	[InlineData("benchy.gcode", false)]
	[InlineData("", false)]
	[InlineData(null, false)]
	public void Only_plain_mp4_names_are_valid(string? name, bool valid)
	{
		Assert.Equal(valid, TimelapseName.IsValid(name));
	}
}
