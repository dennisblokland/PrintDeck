using PrintDeck.Domain.Health;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Measurements of the machine PrintDeck runs on.</summary>
public interface IHostHealth
{
	/// <summary>Space on the disk that holds PrintDeck's data; null when it can't be read.</summary>
	DiskSpace? DataDisk();

	/// <summary>The throttle flags when the host is a Raspberry Pi; null on other machines.</summary>
	Task<PiThrottling?> PiThrottlingAsync(CancellationToken ct);
}
