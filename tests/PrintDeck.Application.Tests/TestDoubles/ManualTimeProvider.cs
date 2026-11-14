namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>Clock that only moves when the test says so. Safe to read from background loops.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
	private readonly Lock _lock = new();
	private DateTimeOffset _now = now;

	public DateTimeOffset Now
	{
		get { lock (_lock) return _now; }
		set { lock (_lock) _now = value; }
	}

	public override DateTimeOffset GetUtcNow() => Now;

	public void Advance(TimeSpan by)
	{
		lock (_lock) _now += by;
	}
}
