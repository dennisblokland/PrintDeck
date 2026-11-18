using PrintDeck.Domain.Health.Enums;

namespace PrintDeck.Domain.Health;

/// <summary>Free and total bytes on the disk that holds PrintDeck's data (uploads, settings, logs).</summary>
public sealed record DiskSpace(long FreeBytes, long TotalBytes)
{
	public const long WarnBelow = 1L << 30;     // 1 GiB
	public const long ErrorBelow = 256L << 20;  // 256 MiB

	public HealthWarning? Check() => FreeBytes switch
	{
		< ErrorBelow => new HealthWarning("disk", HealthSeverity.Error, "The disk is almost full",
			"Uploads, settings and logs may fail to save. Delete old files or free space on the host."),
		< WarnBelow => new HealthWarning("disk", HealthSeverity.Warning, "Disk space is low",
			"Less than 1 GB is free for uploads and logs. Delete old files or free space on the host."),
		_ => null
	};
}
