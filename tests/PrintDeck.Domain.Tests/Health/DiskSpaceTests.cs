using PrintDeck.Domain.Health;
using PrintDeck.Domain.Health.Enums;

namespace PrintDeck.Domain.Tests.Health;

public class DiskSpaceTests
{
	private const long Gb = 1L << 30;
	private const long Mb = 1L << 20;

	[Theory]
	[InlineData(20 * Gb, null)]
	[InlineData(Gb, null)]
	[InlineData(Gb - 1, HealthSeverity.Warning)]
	[InlineData(256 * Mb, HealthSeverity.Warning)]
	[InlineData(256 * Mb - 1, HealthSeverity.Error)]
	[InlineData(0, HealthSeverity.Error)]
	public void Warns_below_1_GB_and_errs_below_256_MB(long free, HealthSeverity? expected) =>
		Assert.Equal(expected, new DiskSpace(free, 32 * Gb).Check()?.Severity);

	[Fact]
	public void Warning_says_what_to_do()
	{
		HealthWarning w = new DiskSpace(100 * Mb, 32 * Gb).Check()!;
		Assert.Equal(("disk", "The disk is almost full"), (w.Id, w.Title));
	}
}
