using PrintDeck.Domain.Timelapse;
using PrintDeck.Domain.Timelapse.Enums;

namespace PrintDeck.Domain.Tests.Timelapse;

public class TimelapseScheduleTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void Timed_takes_the_first_frame_at_once_then_one_per_interval()
	{
		TimelapseSchedule schedule = new(TimelapseMode.Timed, TimeSpan.FromSeconds(10));

		Assert.True(schedule.Tick(_t0, printing: true));
		Assert.False(schedule.Tick(_t0.AddSeconds(5), printing: true));
		Assert.False(schedule.Tick(_t0.AddSeconds(9), printing: true));
		Assert.True(schedule.Tick(_t0.AddSeconds(10), printing: true));
		Assert.False(schedule.Tick(_t0.AddSeconds(15), printing: true));
		Assert.True(schedule.Tick(_t0.AddSeconds(20), printing: true));
	}

	[Fact]
	public void Paused_time_does_not_count()
	{
		TimelapseSchedule schedule = new(TimelapseMode.Timed, TimeSpan.FromSeconds(10));
		Assert.True(schedule.Tick(_t0, printing: true));
		Assert.False(schedule.Tick(_t0.AddSeconds(4), printing: true));

		Assert.False(schedule.Tick(_t0.AddSeconds(5), printing: false));
		Assert.False(schedule.Tick(_t0.AddMinutes(10), printing: false));

		Assert.False(schedule.Tick(_t0.AddMinutes(10).AddSeconds(5), printing: true)); // 4 + 5 s printed
		Assert.True(schedule.Tick(_t0.AddMinutes(10).AddSeconds(6), printing: true));
	}

	[Fact]
	public void Per_layer_takes_one_frame_for_each_new_layer()
	{
		TimelapseSchedule schedule = new(TimelapseMode.Layer, TimeSpan.FromSeconds(10));

		Assert.False(schedule.Tick(_t0, printing: true));
		Assert.True(schedule.OnLayer(1));
		Assert.False(schedule.OnLayer(1));
		Assert.True(schedule.OnLayer(2));
		Assert.False(schedule.OnLayer(0));
	}

	[Fact]
	public void Timed_ignores_layers()
	{
		Assert.False(new TimelapseSchedule(TimelapseMode.Timed, TimeSpan.FromSeconds(1)).OnLayer(1));
	}
}
