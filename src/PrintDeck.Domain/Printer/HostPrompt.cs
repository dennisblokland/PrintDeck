namespace PrintDeck.Domain.Printer;

/// <summary>
/// A question the firmware asks the host (<c>//action:prompt_begin</c> … <c>prompt_show</c>), e.g. after a filament runout.
/// The host answers with <c>M876 S&lt;index of the choice&gt;</c>.
/// </summary>
public sealed record HostPrompt(string Message, IReadOnlyList<string> Choices);
