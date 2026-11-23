using PrintDeck.Plugins.Enums;

namespace PrintDeck.Plugins;

/// <param name="FileName">The print's file, for print and timelapse events.</param>
public sealed record PrinterEvent(PrinterEventKind Kind, DateTimeOffset At, string? FileName = null);
