using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PrintDeck.Infrastructure.Storage;

namespace PrintDeck.Infrastructure.Logging;

/// <summary>
/// PrintDeck's own log in <c>logs/printdeck.log</c>, next to the console output, for downloading from the Terminal page.
/// Levels follow the <c>Logging</c> section of appsettings.json like the console.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider(IOptions<StorageOptions> storage) : ILoggerProvider
{
	public const string FileName = "printdeck.log";

	private readonly RollingLogFile _file = new(Path.Combine(storage.Value.LogsDirectory, FileName), 10 * 1024 * 1024, keep: 3);

	public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _file);

	public Task FlushAsync(CancellationToken ct = default) => _file.FlushAsync(ct);

	public void Dispose() => _file.DisposeAsync().AsTask().GetAwaiter().GetResult();

	private sealed class FileLogger(string category, RollingLogFile file) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel)) return;
			StringBuilder line = new();
			line.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
				.Append(' ').Append(Level(logLevel)).Append(' ').Append(category).Append(": ").Append(formatter(state, exception));
			if (exception is not null) line.AppendLine().Append(exception);
			file.Write(line.ToString());
		}

		private static string Level(LogLevel level) => level switch
		{
			LogLevel.Trace => "trce",
			LogLevel.Debug => "dbug",
			LogLevel.Information => "info",
			LogLevel.Warning => "warn",
			LogLevel.Error => "fail",
			_ => "crit"
		};
	}
}
