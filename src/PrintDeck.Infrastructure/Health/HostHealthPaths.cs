namespace PrintDeck.Infrastructure.Health;

/// <summary>Where a Raspberry Pi tells its model and throttle flags; tests point these at temp files.</summary>
public sealed record HostHealthPaths(
	string ModelFile = "/proc/device-tree/model",
	string ThrottledFile = "/sys/devices/platform/soc/soc:firmware/get_throttled");
