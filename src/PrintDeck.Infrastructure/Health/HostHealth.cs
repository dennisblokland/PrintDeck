using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Health;
using PrintDeck.Infrastructure.Processes;

namespace PrintDeck.Infrastructure.Health;

/// <summary>
/// Disk space through <see cref="DriveInfo"/>; on a Raspberry Pi, the throttle flags from sysfs, or from
/// <c>vcgencmd get_throttled</c> on kernels without that file. Pi readings are kept for a few seconds,
/// since every open page asks.
/// </summary>
public sealed class HostHealth(string dataDirectory, ICommandRunner runner, TimeProvider time, HostHealthPaths? paths = null) : IHostHealth
{
	private static readonly TimeSpan _cacheFor = TimeSpan.FromSeconds(15);

	private readonly HostHealthPaths _paths = paths ?? new HostHealthPaths();
	private readonly SemaphoreSlim _lock = new(1, 1);
	private (DateTimeOffset At, PiThrottling? Value)? _cached;

	public DiskSpace? DataDisk()
	{
		try
		{
			string path = Directory.Exists(dataDirectory) ? dataDirectory : Path.GetPathRoot(Path.GetFullPath(dataDirectory)) ?? "/";
			DriveInfo drive = new(path);
			return new DiskSpace(drive.AvailableFreeSpace, drive.TotalSize);
		}
		catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	public async Task<PiThrottling?> PiThrottlingAsync(CancellationToken ct)
	{
		await _lock.WaitAsync(ct);
		try
		{
			if (_cached is { } c && time.GetUtcNow() - c.At < _cacheFor) return c.Value;
			PiThrottling? value = await ReadAsync(ct);
			_cached = (time.GetUtcNow(), value);
			return value;
		}
		finally
		{
			_lock.Release();
		}
	}

	private async Task<PiThrottling?> ReadAsync(CancellationToken ct)
	{
		if (!IsRaspberryPi()) return null;
		try
		{
			if (File.Exists(_paths.ThrottledFile))
				return PiThrottling.Parse(await File.ReadAllTextAsync(_paths.ThrottledFile, ct));

			CommandOutput? output = await runner.RunAsync("vcgencmd", ["get_throttled"], TimeSpan.FromSeconds(3), ct);
			return output is { ExitCode: 0 } ? PiThrottling.Parse(output.Output) : null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
		{
			return null;
		}
	}

	private bool IsRaspberryPi()
	{
		try
		{
			return File.Exists(_paths.ModelFile) && File.ReadAllText(_paths.ModelFile).Contains("Raspberry Pi", StringComparison.Ordinal);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}
}
