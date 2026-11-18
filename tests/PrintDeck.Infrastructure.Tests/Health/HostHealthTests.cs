using PrintDeck.Domain.Health;
using PrintDeck.Infrastructure.Health;
using PrintDeck.Infrastructure.Processes;
using PrintDeck.Infrastructure.Tests.TestSupport;

namespace PrintDeck.Infrastructure.Tests.Health;

public sealed class HostHealthTests : IDisposable
{
	private readonly TempDirectory _dir = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public void Dispose() => _dir.Dispose();

	private sealed class CountingRunner(CommandOutput? answer) : ICommandRunner
	{
		public int Runs { get; private set; }

		public Task<CommandOutput?> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
		{
			Runs++;
			Assert.Equal(("vcgencmd", "get_throttled"), (program, Assert.Single(arguments)));
			return Task.FromResult(answer);
		}
	}

	private sealed class Clock : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
		public override DateTimeOffset GetUtcNow() => Now;
	}

	private string File(string name, string? content)
	{
		string path = Path.Combine(_dir.Path, name);
		if (content is not null) System.IO.File.WriteAllText(path, content);
		return path;
	}

	private HostHealth Health(string? model, string? throttled, ICommandRunner runner, TimeProvider? time = null) =>
		new(_dir.Path, runner, time ?? TimeProvider.System, new HostHealthPaths(File("model", model), File("get_throttled", throttled)));

	[Fact]
	public void Reads_the_data_disk()
	{
		DiskSpace disk = Health(null, null, new CountingRunner(null)).DataDisk()!;

		Assert.True(disk.TotalBytes > 0);
		Assert.InRange(disk.FreeBytes, 0, disk.TotalBytes);
	}

	[Fact]
	public async Task Not_a_pi_reads_nothing()
	{
		CountingRunner runner = new(null);

		Assert.Null(await Health("Some PC", "50005", runner).PiThrottlingAsync(Ct));
		Assert.Null(await Health(null, "50005", runner).PiThrottlingAsync(Ct));
		Assert.Equal(0, runner.Runs);
	}

	[Fact]
	public async Task A_pi_reads_sysfs_first()
	{
		CountingRunner runner = new(null);

		PiThrottling? pi = await Health("Raspberry Pi 4 Model B Rev 1.4\0", "50005\n", runner).PiThrottlingAsync(Ct);

		Assert.Equal(0x50005, pi!.Flags);
		Assert.Equal(0, runner.Runs);
	}

	[Fact]
	public async Task A_pi_without_the_sysfs_file_asks_vcgencmd()
	{
		CountingRunner runner = new(new CommandOutput(0, "throttled=0x10000\n", ""));

		PiThrottling? pi = await Health("Raspberry Pi 3 Model B Plus Rev 1.3", null, runner).PiThrottlingAsync(Ct);

		Assert.Equal(0x10000, pi!.Flags);
		Assert.Equal(1, runner.Runs);
	}

	[Fact]
	public async Task Missing_vcgencmd_reads_nothing()
	{
		Assert.Null(await Health("Raspberry Pi 5", null, new CountingRunner(null)).PiThrottlingAsync(Ct));
	}

	[Fact]
	public async Task Keeps_a_reading_for_a_few_seconds()
	{
		Clock clock = new();
		CountingRunner runner = new(new CommandOutput(0, "throttled=0x0", ""));
		HostHealth health = Health("Raspberry Pi 4", null, runner, clock);

		await health.PiThrottlingAsync(Ct);
		clock.Now += TimeSpan.FromSeconds(10);
		await health.PiThrottlingAsync(Ct);
		Assert.Equal(1, runner.Runs);

		clock.Now += TimeSpan.FromSeconds(10);
		await health.PiThrottlingAsync(Ct);
		Assert.Equal(2, runner.Runs);
	}
}
