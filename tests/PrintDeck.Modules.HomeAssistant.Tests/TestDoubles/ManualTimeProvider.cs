namespace PrintDeck.Modules.HomeAssistant.Tests.TestDoubles;

/// <summary>Clock that only moves when the test says so; its timers fire when <see cref="Advance"/> passes them.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
	private readonly Lock _gate = new();
	private readonly List<ManualTimer> _timers = [];
	private DateTimeOffset _now = now;

	public override DateTimeOffset GetUtcNow()
	{
		lock (_gate) return _now;
	}

	public int ActiveTimers
	{
		get { lock (_gate) return _timers.Count(t => t.Due is not null); }
	}

	public void Advance(TimeSpan by)
	{
		List<ManualTimer> due;
		lock (_gate)
		{
			_now += by;
			due = _timers.Where(t => t.Due <= _now).ToList();
			foreach (ManualTimer t in due) t.Due = null;
		}
		foreach (ManualTimer t in due) t.Fire();
	}

	public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
	{
		ManualTimer timer = new(this, callback, state);
		lock (_gate)
		{
			_timers.Add(timer);
			timer.Due = dueTime == Timeout.InfiniteTimeSpan ? null : _now + dueTime;
		}
		if (dueTime == TimeSpan.Zero)
		{
			lock (_gate) timer.Due = null;
			ThreadPool.QueueUserWorkItem(_ => timer.Fire());
		}
		return timer;
	}

	private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
	{
		public DateTimeOffset? Due { get; set; }

		public void Fire() => callback(state);

		public bool Change(TimeSpan dueTime, TimeSpan period)
		{
			lock (owner._gate) Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
			return true;
		}

		public void Dispose()
		{
			lock (owner._gate)
			{
				Due = null;
				owner._timers.Remove(this);
			}
		}

		public ValueTask DisposeAsync()
		{
			Dispose();
			return ValueTask.CompletedTask;
		}
	}
}
