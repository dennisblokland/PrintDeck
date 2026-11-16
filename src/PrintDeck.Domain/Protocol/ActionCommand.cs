namespace PrintDeck.Domain.Protocol;

/// <summary>
/// A host action command from the firmware: "//action:pause", "// action:prompt_begin Paused", …
/// Marlin sends them with HOST_ACTION_COMMANDS; Prusa firmware sends pause, resume and cancel from its LCD.
/// </summary>
/// <param name="Name">Lower-case action, e.g. "pause", "prompt_choice".</param>
/// <param name="Argument">Text after the action, trimmed; empty when there is none.</param>
public sealed record ActionCommand(string Name, string Argument)
{
	private const string _marker = "action:";

	public static ActionCommand? TryParse(string line)
	{
		ReadOnlySpan<char> text = line.AsSpan().Trim();
		if (!text.StartsWith("//")) return null;

		text = text[2..].TrimStart();
		if (!text.StartsWith(_marker, StringComparison.OrdinalIgnoreCase)) return null;

		text = text[_marker.Length..].Trim();
		if (text.IsEmpty) return null;

		int space = text.IndexOf(' ');
		string name = (space < 0 ? text : text[..space]).ToString().ToLowerInvariant();
		string argument = space < 0 ? "" : text[(space + 1)..].Trim().ToString();
		return new ActionCommand(name, argument);
	}
}
