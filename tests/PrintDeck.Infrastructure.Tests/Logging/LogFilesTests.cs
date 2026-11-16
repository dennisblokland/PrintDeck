using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PrintDeck.Domain.Logs;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;
using PrintDeck.Infrastructure.Logging;
using PrintDeck.Infrastructure.Storage;
using PrintDeck.Infrastructure.Tests.TestSupport;

namespace PrintDeck.Infrastructure.Tests.Logging;

/// <summary>The serial log, the app log and the directory listing behind the Terminal page's "Log files".</summary>
public sealed class LogFilesTests : IDisposable
{
	private readonly TempDirectory _dir = new();
	private readonly IOptions<StorageOptions> _storage;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public LogFilesTests() => _storage = Options.Create(new StorageOptions { DataDirectory = _dir.Path });

	public void Dispose() => _dir.Dispose();

	private string Logs => _storage.Value.LogsDirectory;

	private static TerminalEntry Entry(TerminalDirection direction, string text) =>
		new(1, new DateTimeOffset(2026, 11, 16, 18, 0, 0, 123, TimeSpan.Zero), direction, text);

	[Fact]
	public async Task Serial_log_records_only_while_switched_on()
	{
		await using FileSerialLog log = new(_storage);
		log.Write(Entry(TerminalDirection.Sent, "N1 M105*39")); // off: dropped

		log.SetEnabled(true);
		log.Write(Entry(TerminalDirection.Sent, "N2 M105*36"));
		log.Write(Entry(TerminalDirection.Received, "ok T:21.0 /0.0"));
		log.Write(Entry(TerminalDirection.Error, "Error:checksum mismatch"));
		log.SetEnabled(false);
		log.Write(Entry(TerminalDirection.Sent, "N3 M105*37"));
		await log.FlushAsync(Ct);

		string[] lines = await File.ReadAllLinesAsync(Path.Combine(Logs, FileSerialLog.FileName), Ct);
		Assert.Equal(["- Serial log on", "> N2 M105*36", "< ok T:21.0 /0.0", "! Error:checksum mismatch", "- Serial log off"],
			lines.Select(l => l[24..]));
		Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{3} ", lines[1]);
	}

	[Fact]
	public async Task App_log_writes_level_category_message_and_exception()
	{
		using FileLoggerProvider provider = new(_storage);
		ILogger logger = provider.CreateLogger("PrintDeck.Test");
		logger.LogWarning("Port {Port} vanished", "/dev/ttyACM0");
		logger.LogError(new InvalidOperationException("boom"), "Send loop crashed");
		await provider.FlushAsync(Ct);

		string text = await File.ReadAllTextAsync(Path.Combine(Logs, FileLoggerProvider.FileName), Ct);
		Assert.Contains(" warn PrintDeck.Test: Port /dev/ttyACM0 vanished", text);
		Assert.Contains(" fail PrintDeck.Test: Send loop crashed", text);
		Assert.Contains("System.InvalidOperationException: boom", text);
	}

	[Fact]
	public async Task Lists_log_files_and_their_rolled_copies_only()
	{
		Directory.CreateDirectory(Logs);
		await File.WriteAllTextAsync(Path.Combine(Logs, "serial.log"), "abc", Ct);
		await File.WriteAllTextAsync(Path.Combine(Logs, "printdeck.log.2"), "x", Ct);
		await File.WriteAllTextAsync(Path.Combine(Logs, "notes.txt"), "no", Ct);
		await File.WriteAllTextAsync(Path.Combine(Logs, "printdeck.log.bak"), "no", Ct);

		IReadOnlyList<LogFile> files = new LogDirectoryFiles(_storage).List();

		Assert.Equal(["printdeck.log.2", "serial.log"], files.Select(f => f.Name));
		Assert.Equal(3, files.Single(f => f.Name == "serial.log").SizeBytes);
	}

	[Fact]
	public void No_logs_directory_means_no_files() => Assert.Empty(new LogDirectoryFiles(_storage).List());

	[Theory]
	[InlineData("../settings.json")]
	[InlineData("..")]
	[InlineData("notes.txt")]
	[InlineData("missing.log")]
	public async Task Opens_only_listed_files(string name)
	{
		Directory.CreateDirectory(Logs);
		await File.WriteAllTextAsync(Path.Combine(Logs, "notes.txt"), "secret", Ct);
		await File.WriteAllTextAsync(Path.Combine(_dir.Path, "settings.json"), "{}", Ct);

		Assert.Null(new LogDirectoryFiles(_storage).OpenRead(name));
	}

	[Fact]
	public async Task Opens_a_log_that_is_still_being_written()
	{
		await using FileSerialLog log = new(_storage);
		log.SetEnabled(true);
		await log.FlushAsync(Ct);

		await using Stream? stream = new LogDirectoryFiles(_storage).OpenRead(FileSerialLog.FileName);
		Assert.EndsWith("- Serial log on", (await new StreamReader(stream!).ReadToEndAsync(Ct)).Trim());
	}
}
