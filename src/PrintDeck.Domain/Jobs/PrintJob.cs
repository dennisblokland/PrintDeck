using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Domain.Jobs;

/// <summary>A single print of a G-code file. Owns progress and timing rules.</summary>
public sealed class PrintJob
{
	private DateTimeOffset? _pausedAt;
	private TimeSpan _pausedTotal;

	public PrintJob(string fileName, long totalBytes, TimeSpan? slicerEstimate, DateTimeOffset now)
	{
		if (totalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(totalBytes), "File is empty.");
		FileName = fileName;
		TotalBytes = totalBytes;
		SlicerEstimate = slicerEstimate;
		StartedAt = now;
	}

	public string FileName { get; }
	public long TotalBytes { get; }
	public TimeSpan? SlicerEstimate { get; }
	public DateTimeOffset StartedAt { get; }
	public DateTimeOffset? EndedAt { get; private set; }
	public long BytesProcessed { get; private set; }
	public long LinesSent { get; private set; }
	public JobStatus Status { get; private set; } = JobStatus.Printing;

	public bool IsActive => Status is JobStatus.Printing or JobStatus.Paused;
	public double Progress => Math.Clamp((double) BytesProcessed / TotalBytes, 0, 1);

	public TimeSpan Elapsed(DateTimeOffset now)
	{
		DateTimeOffset end = EndedAt ?? now;
		TimeSpan paused = _pausedTotal + (_pausedAt is { } p ? end - p : TimeSpan.Zero);
		return end - StartedAt - paused;
	}

	/// <summary>Blend slicer estimate with linear extrapolation as the print advances.</summary>
	public TimeSpan? EstimatedRemaining(DateTimeOffset now)
	{
		if (!IsActive) return TimeSpan.Zero;
		TimeSpan elapsed = Elapsed(now);
		double p = Progress;
		TimeSpan? linear = p > 0.01 ? TimeSpan.FromSeconds(elapsed.TotalSeconds / p * (1 - p)) : null;
		TimeSpan? slicer = SlicerEstimate is { } s ? s * (1 - p) : null;

		return (slicer, linear) switch
		{
			({ } a, { } b) => TimeSpan.FromSeconds(a.TotalSeconds * (1 - p) + b.TotalSeconds * p),
			({ } a, null) => a,
			(null, { } b) => b,
			_ => null
		};
	}

	public void Advance(long bytes)
	{
		BytesProcessed = Math.Min(TotalBytes, BytesProcessed + bytes);
		LinesSent++;
	}

	public void Pause(DateTimeOffset now)
	{
		if (Status != JobStatus.Printing) throw new InvalidOperationException("Only a running job can be paused.");
		Status = JobStatus.Paused;
		_pausedAt = now;
	}

	public void Resume(DateTimeOffset now)
	{
		if (Status != JobStatus.Paused) throw new InvalidOperationException("Only a paused job can be resumed.");
		_pausedTotal += now - _pausedAt!.Value;
		_pausedAt = null;
		Status = JobStatus.Printing;
	}

	public void Complete(DateTimeOffset now) => End(JobStatus.Completed, now);
	public void Cancel(DateTimeOffset now) => End(JobStatus.Cancelled, now);
	public void Fail(DateTimeOffset now) => End(JobStatus.Failed, now);

	private void End(JobStatus status, DateTimeOffset now)
	{
		if (!IsActive) return;
		if (_pausedAt is { } p) { _pausedTotal += now - p; _pausedAt = null; }
		if (status == JobStatus.Completed) BytesProcessed = TotalBytes;
		Status = status;
		EndedAt = now;
	}
}
