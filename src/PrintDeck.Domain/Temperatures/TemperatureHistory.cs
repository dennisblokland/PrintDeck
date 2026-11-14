using PrintDeck.Domain.Common;

namespace PrintDeck.Domain.Temperatures;

public sealed class TemperatureHistory(int capacity = 900)
{
	private readonly RingBuffer<TemperatureSample> _samples = new(capacity);

	public TemperatureSample? Latest { get; private set; }

	public void Record(TemperatureSample sample)
	{
		_samples.Add(sample);
		Latest = sample;
	}

	public IReadOnlyList<TemperatureSample> Since(TimeSpan window, DateTimeOffset now) =>
		_samples.Snapshot().Where(s => now - s.At <= window).ToList();

	public void Clear() { _samples.Clear(); Latest = null; }
}
