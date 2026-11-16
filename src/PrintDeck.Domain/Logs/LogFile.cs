namespace PrintDeck.Domain.Logs;

/// <summary>A log file PrintDeck wrote, e.g. <c>serial.log</c> or the rolled-over <c>printdeck.log.1</c>.</summary>
public sealed record LogFile(string Name, long SizeBytes, DateTimeOffset ModifiedAt);
