namespace PrintDeck.Domain.Printer;

/// <summary>Something the printer wants the user to know (<c>//action:notification</c>, filament runout, pause from the LCD).</summary>
public sealed record PrinterNotification(DateTimeOffset At, string Text);
