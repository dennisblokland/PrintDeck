namespace PrintDeck.Domain.Files;

/// <summary>How often a file printed, and how its latest print ended.</summary>
public sealed record PrintStats(int Completed, int Unsuccessful, PrintRecord? Last)
{
	public static readonly PrintStats None = new(0, 0, null);

	public static PrintStats From(IEnumerable<PrintRecord> prints)
	{
		int completed = 0, unsuccessful = 0;
		PrintRecord? last = null;
		foreach (PrintRecord p in prints)
		{
			if (p.Succeeded) completed++;
			else unsuccessful++;
			if (last is null || p.EndedAt > last.EndedAt) last = p;
		}
		return new PrintStats(completed, unsuccessful, last);
	}
}
