using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Files.Enums;
using PrintDeck.Domain.Protocol;

namespace PrintDeck.Domain.Settings;

/// <summary>A button on the Control page that sends a piece of G-code, which may include snippets.</summary>
/// <param name="Label">What the button says.</param>
/// <param name="Gcode">One command per line; <c>{% snippet 'name' %}</c> on its own line includes a snippet.</param>
/// <param name="Icon">One of <see cref="Icons"/>, or null for none.</param>
/// <param name="Confirm">Ask before sending.</param>
/// <param name="IdleOnly">Only when the printer is idle or paused, like jogging; never mid-print.</param>
public sealed record CustomButton(string Label, string Gcode, string? Icon = null, bool Confirm = false, bool IdleOnly = true)
{
	public const int MaxLabelLength = 40;
	public const int MaxButtons = 40;

	/// <summary>Icons a button can show: names from the web UI's icon set.</summary>
	public static readonly IReadOnlyList<string> Icons =
	[
		"play", "pause", "stop", "home", "move", "up", "down", "crosshair", "scan", "layers",
		"flame", "bed", "thermo", "fan", "droplet", "spool", "power", "refresh", "bell", "camera", "cube", "sliders"
	];

	/// <summary>Examples for a Prusa MK3S, offered in Settings; not added by default.</summary>
	public static readonly IReadOnlyList<CustomButton> PrusaExamples =
	[
		new("Mesh bed level", "G28 W\nG80", "scan", Confirm: true),
		new("Filament change", "M600", "spool", IdleOnly: false),
		new("Raise to Z 150", "G90\nG1 Z150 F600", "up"),
		new("Motors off", "M84", "power")
	];

	/// <summary>
	/// The lines to send, with snippets filled in. Fails on an unknown snippet, on nothing to send, and on the commands
	/// the file check calls errors: ones that break the connection (M112, M110, M28/M29, M997) and unfilled placeholders.
	/// </summary>
	public Result<IReadOnlyList<string>> Commands(IReadOnlyList<GcodeSnippet> snippets)
	{
		Result<IReadOnlyList<string>> expanded = GcodeScript.Expand(Gcode, snippets);
		if (!expanded.IsSuccess) return expanded;

		GcodeProblemCheck check = new(new PrinterProfile());
		foreach (string line in expanded.Value!) check.Inspect(line);
		if (check.Finish().FirstOrDefault(p => p.Severity == GcodeProblemSeverity.Error) is { } error)
			return Result.Failure<IReadOnlyList<string>>(error.Message);

		List<string> commands = expanded.Value!.Where(l => GcodeLine.Clean(l) is not null).ToList();
		return commands.Count == 0
			? Result.Failure<IReadOnlyList<string>>("There is no G-code to send.")
			: Result.Success<IReadOnlyList<string>>(commands);
	}

	/// <summary>A short reason when the button can't be saved, otherwise null.</summary>
	public string? Problem(IReadOnlyList<GcodeSnippet> snippets)
	{
		string label = Label.Trim();
		if (label.Length == 0) return "Every button needs a label.";
		if (label.Length > MaxLabelLength) return $"Button labels can be at most {MaxLabelLength} characters.";
		if (Icon is not null && !Icons.Contains(Icon)) return $"{label}: unknown icon '{Icon}'.";
		return Commands(snippets) is { IsSuccess: false } failed ? $"{label}: {failed.Error}" : null;
	}
}
