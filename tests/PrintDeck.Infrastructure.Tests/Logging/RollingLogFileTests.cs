using PrintDeck.Infrastructure.Logging;
using PrintDeck.Infrastructure.Tests.TestSupport;

namespace PrintDeck.Infrastructure.Tests.Logging;

public sealed class RollingLogFileTests : IDisposable
{
	private readonly TempDirectory _dir = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public void Dispose() => _dir.Dispose();

	private string FilePath => Path.Combine(_dir.Path, "logs", "app.log");

	[Fact]
	public async Task Appends_lines_and_creates_the_directory()
	{
		await using RollingLogFile log = new(FilePath, 1024, keep: 2);
		log.Write("one");
		log.Write("two");
		await log.FlushAsync(Ct);

		Assert.Equal(["one", "two"], await File.ReadAllLinesAsync(FilePath, Ct));
	}

	[Fact]
	public async Task Empty_lines_are_not_written()
	{
		await using RollingLogFile log = new(FilePath, 1024, keep: 2);
		log.Write("");
		log.Write("one");
		await log.FlushAsync(Ct);
		Assert.Equal(["one"], await File.ReadAllLinesAsync(FilePath, Ct));
	}

	[Fact]
	public async Task Rolls_over_and_keeps_only_the_newest_copies()
	{
		await using RollingLogFile log = new(FilePath, maxBytes: 10, keep: 2);
		foreach (string line in new[] { "first-line", "second-line", "third-line", "fourth-line" })
		{
			log.Write(line);
			await log.FlushAsync(Ct);
		}

		Assert.Equal(["fourth-line"], await File.ReadAllLinesAsync(FilePath, Ct));
		Assert.Equal(["third-line"], await File.ReadAllLinesAsync(FilePath + ".1", Ct));
		Assert.Equal(["second-line"], await File.ReadAllLinesAsync(FilePath + ".2", Ct));
		Assert.False(File.Exists(FilePath + ".3"));
	}

	[Fact]
	public async Task The_file_can_be_read_while_it_is_written()
	{
		await using RollingLogFile log = new(FilePath, 1024, keep: 1);
		log.Write("busy");
		await log.FlushAsync(Ct);

		await using FileStream read = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		Assert.Equal("busy", (await new StreamReader(read).ReadToEndAsync(Ct)).Trim());
	}

	[Fact]
	public async Task Dispose_writes_what_is_pending_and_later_writes_are_dropped()
	{
		RollingLogFile log = new(FilePath, 1024, keep: 1);
		log.Write("last words");
		await log.DisposeAsync();
		log.Write("too late");
		await log.FlushAsync(Ct);

		Assert.Equal(["last words"], await File.ReadAllLinesAsync(FilePath, Ct));
	}
}
