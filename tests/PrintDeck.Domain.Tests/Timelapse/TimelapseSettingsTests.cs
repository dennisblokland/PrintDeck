using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Timelapse;
using PrintDeck.Domain.Timelapse.Enums;

namespace PrintDeck.Domain.Tests.Timelapse;

public class TimelapseSettingsTests
{
	[Fact]
	public void Off_by_default_every_ten_seconds_at_25_fps()
	{
		Assert.Equal(new TimelapseSettings(TimelapseMode.Off, 10, 25), new HostSettings().Timelapse);
		Assert.Null(new TimelapseSettings().Problem());
	}

	[Theory]
	[InlineData(0, 25, "The timelapse interval must be 1–3600 seconds.")]
	[InlineData(3601, 25, "The timelapse interval must be 1–3600 seconds.")]
	[InlineData(10, 0, "The timelapse frame rate must be 1–60 frames per second.")]
	[InlineData(10, 61, "The timelapse frame rate must be 1–60 frames per second.")]
	public void Out_of_range_values_are_a_problem(int interval, int frameRate, string problem)
	{
		Assert.Equal(problem, new TimelapseSettings(TimelapseMode.Timed, interval, frameRate).Problem());
	}

	[Fact]
	public void An_unknown_mode_is_a_problem()
	{
		Assert.Equal("Pick a timelapse mode.", new TimelapseSettings((TimelapseMode) 9).Problem());
	}

	[Fact]
	public void A_hand_edited_file_is_repaired()
	{
		HostSettings s = new HostSettings { Timelapse = new TimelapseSettings((TimelapseMode) 9, 0, 500) }.Normalized();

		Assert.Equal(new TimelapseSettings(TimelapseMode.Off, 1, 60), s.Timelapse);
		Assert.Equal(new TimelapseSettings(), (new HostSettings { Timelapse = null! }).Normalized().Timelapse);
	}
}
