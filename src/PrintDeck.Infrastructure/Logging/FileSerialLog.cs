using System.Globalization;

using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;
using PrintDeck.Infrastructure.Storage;

namespace PrintDeck.Infrastructure.Logging;

/// <summary>
/// Every line to and from the printer in <c>logs/serial.log</c>, while switched on:
/// <c>2026-11-16 18:00:00.123 &gt; N12 G28*21</c> (&gt; sent, &lt; received, ! error, - PrintDeck).
/// </summary>
public sealed class FileSerialLog(IOptions<StorageOptions> storage) : ISerialLog, IAsyncDisposable
{
	public const string FileName = "serial.log";

	private readonly RollingLogFile _file = new(Path.Combine(storage.Value.LogsDirectory, FileName), 20 * 1024 * 1024, keep: 2);
	private volatile bool _enabled;

	public bool Enabled => _enabled;

	public void SetEnabled(bool enabled)
	{
		if (_enabled == enabled) return;
		if (!enabled) _file.Write(Line(DateTimeOffset.UtcNow, "-", "Serial log off"));
		_enabled = enabled;
		if (enabled) _file.Write(Line(DateTimeOffset.UtcNow, "-", "Serial log on"));
	}

	public void Write(TerminalEntry entry)
	{
		if (!_enabled) return;
		string direction = entry.Direction switch
		{
			TerminalDirection.Sent => ">",
			TerminalDirection.Received => "<",
			TerminalDirection.Error => "!",
			_ => "-"
		};
		_file.Write(Line(entry.At, direction, entry.Text));
	}

	public Task FlushAsync(CancellationToken ct = default) => _file.FlushAsync(ct);

	private static string Line(DateTimeOffset at, string direction, string text) =>
		$"{at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} {direction} {text}";

	public ValueTask DisposeAsync() => _file.DisposeAsync();
}
