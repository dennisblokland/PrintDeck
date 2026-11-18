using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Health;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeHostHealth : IHostHealth
{
	public DiskSpace? Disk { get; set; } = new(20L << 30, 32L << 30);
	public PiThrottling? Pi { get; set; }

	public DiskSpace? DataDisk() => Disk;
	public Task<PiThrottling?> PiThrottlingAsync(CancellationToken ct) => Task.FromResult(Pi);
}
