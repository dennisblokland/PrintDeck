using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Domain.Terminal;

public sealed record TerminalEntry(long Id, DateTimeOffset At, TerminalDirection Direction, string Text);
