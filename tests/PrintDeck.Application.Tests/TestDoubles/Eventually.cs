namespace PrintDeck.Application.Tests.TestDoubles;

internal static class Eventually
{
	/// <summary>Polls until the condition holds; fails the test after <paramref name="seconds"/>.</summary>
	public static async Task Until(Func<bool> condition, int seconds = 10, string? because = null)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
		while (!condition())
		{
			if (DateTime.UtcNow > deadline) throw new TimeoutException(because ?? "Condition not met in time.");
			await Task.Delay(20, TestContext.Current.CancellationToken);
		}
	}
}
