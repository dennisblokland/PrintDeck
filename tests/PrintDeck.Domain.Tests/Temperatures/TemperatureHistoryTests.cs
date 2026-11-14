using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Domain.Tests.Temperatures;

public class TemperatureHistoryTests
{
	private static readonly DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

	private static TemperatureSample Sample(TimeSpan ago, double t0 = 20) => new(_now - ago, [new HeaterReading("T0", t0, 0)]);

	[Fact]
	public void Starts_empty()
	{
		TemperatureHistory history = new();
		Assert.Null(history.Latest);
		Assert.Empty(history.Since(TimeSpan.FromDays(1), _now));
	}

	[Fact]
	public void Record_updates_latest()
	{
		TemperatureHistory history = new();
		TemperatureSample sample = Sample(TimeSpan.Zero);
		history.Record(sample);
		Assert.Same(sample, history.Latest);
	}

	[Fact]
	public void Since_returns_samples_inside_the_window()
	{
		TemperatureHistory history = new();
		history.Record(Sample(TimeSpan.FromMinutes(10), 1));
		history.Record(Sample(TimeSpan.FromMinutes(5), 2));
		history.Record(Sample(TimeSpan.FromMinutes(1), 3));

		IReadOnlyList<TemperatureSample> recent = history.Since(TimeSpan.FromMinutes(5), _now);
		Assert.Equal([2.0, 3.0], recent.Select(s => s.Heaters[0].Actual));
	}

	[Fact]
	public void Drops_the_oldest_samples_beyond_capacity()
	{
		TemperatureHistory history = new(capacity: 2);
		for (int i = 1; i <= 3; i++) history.Record(Sample(TimeSpan.FromSeconds(10 - i), i));
		Assert.Equal([2.0, 3.0], history.Since(TimeSpan.FromHours(1), _now).Select(s => s.Heaters[0].Actual));
	}

	[Fact]
	public void Clear_forgets_everything()
	{
		TemperatureHistory history = new();
		history.Record(Sample(TimeSpan.Zero));
		history.Clear();
		Assert.Null(history.Latest);
		Assert.Empty(history.Since(TimeSpan.FromDays(1), _now));
	}
}
