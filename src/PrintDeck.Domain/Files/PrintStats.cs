namespace PrintDeck.Domain.Files;

/// <summary>Totals over a set of finished prints: one file's, or all of them.</summary>
public sealed record PrintStats(
	int Completed,
	int Unsuccessful,
	PrintRecord? Last,
	TimeSpan TotalPrintTime = default,
	double TotalFilamentMm = 0)
{
	public static readonly PrintStats None = new(0, 0, null);

	public int Total => Completed + Unsuccessful;

	/// <summary>Completed share of all prints, 0–1; null without prints.</summary>
	public double? SuccessRate => Total > 0 ? (double) Completed / Total : null;

	public static PrintStats From(IEnumerable<PrintRecord> prints)
	{
		int completed = 0, unsuccessful = 0;
		TimeSpan time = TimeSpan.Zero;
		double filament = 0;
		PrintRecord? last = null;
		foreach (PrintRecord p in prints)
		{
			if (p.Succeeded) completed++;
			else unsuccessful++;
			time += p.PrintTime;
			filament += p.FilamentMm ?? 0;
			if (last is null || p.EndedAt > last.EndedAt) last = p;
		}
		return new PrintStats(completed, unsuccessful, last, time, filament);
	}
}
