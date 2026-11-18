using System.Globalization;
using System.Text;

using PrintDeck.Domain.Camera.Enums;
using PrintDeck.Infrastructure.Camera;

namespace PrintDeck.Infrastructure.Virtual;

/// <summary>
/// Plays <c>v4l2-ctl</c> for a made-up USB webcam, so camera controls can be tried without one. Answers
/// <c>--list-ctrls-menus</c> and <c>--set-ctrl=name=value</c> like the real tool, including the controls that turn
/// inactive while their automatic mode is on. The values live as long as PrintDeck runs, like a camera's do until
/// it loses power.
/// </summary>
public sealed class VirtualV4l2Ctl : ICommandRunner
{
	public const string DeviceName = "VIRTUAL";

	private sealed record Spec(string Name, CameraControlType Type, long Min, long Max, long Step, long Default, (long, string)[]? Menu = null, string? ActiveWhen = null);

	private static readonly Spec[] _specs =
	[
		new("brightness", CameraControlType.Integer, -64, 64, 1, 0),
		new("contrast", CameraControlType.Integer, 0, 64, 1, 32),
		new("saturation", CameraControlType.Integer, 0, 128, 1, 64),
		new("sharpness", CameraControlType.Integer, 0, 6, 1, 3),
		new("white_balance_automatic", CameraControlType.Boolean, 0, 1, 1, 1),
		new("white_balance_temperature", CameraControlType.Integer, 2800, 6500, 10, 4600, ActiveWhen: "white_balance_automatic=0"),
		new("power_line_frequency", CameraControlType.Menu, 0, 2, 1, 1, [(0, "Disabled"), (1, "50 Hz"), (2, "60 Hz")]),
		new("auto_exposure", CameraControlType.Menu, 0, 3, 1, 3, [(1, "Manual Mode"), (3, "Aperture Priority Mode")]),
		new("exposure_time_absolute", CameraControlType.Integer, 3, 2047, 1, 250, ActiveWhen: "auto_exposure=1"),
		new("focus_automatic_continuous", CameraControlType.Boolean, 0, 1, 1, 1),
		new("focus_absolute", CameraControlType.Integer, 0, 250, 5, 0, ActiveWhen: "focus_automatic_continuous=0"),
	];

	private readonly Lock _lock = new();
	private readonly Dictionary<string, long> _values = _specs.ToDictionary(s => s.Name, s => s.Default);

	public Task<CommandOutput?> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
	{
		if (arguments is not ["-d", DeviceName, string action])
			return Done(1, "", "Usage: -d VIRTUAL --list-ctrls-menus | --set-ctrl=<name>=<value>\n");
		if (action == "--list-ctrls-menus") return Done(0, List(), "");
		if (action.StartsWith("--set-ctrl=", StringComparison.Ordinal)) return Set(action["--set-ctrl=".Length..]);
		return Done(1, "", $"unknown option {action}\n");
	}

	private static Task<CommandOutput?> Done(int exit, string output, string error) =>
		Task.FromResult<CommandOutput?>(new CommandOutput(exit, output, error));

	private Task<CommandOutput?> Set(string assignment)
	{
		string[] parts = assignment.Split('=', 2);
		if (parts.Length != 2 || _specs.FirstOrDefault(s => s.Name == parts[0]) is not { } spec)
			return Done(255, "", $"unknown control '{parts[0]}'\n");
		if (!long.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
			return Done(255, "", $"invalid value '{parts[1]}'\n");

		bool fits = spec.Type switch
		{
			CameraControlType.Menu => spec.Menu!.Any(m => m.Item1 == value),
			_ => value >= spec.Min && value <= spec.Max && (value - spec.Min) % spec.Step == 0
		};
		if (!fits)
			return Done(255, "", "VIDIOC_S_EXT_CTRLS: failed: Numerical result out of range\nError setting controls: Numerical result out of range\n");

		lock (_lock) _values[spec.Name] = value;
		return Done(0, "", "");
	}

	private string List()
	{
		StringBuilder text = new("\nUser Controls\n\n");
		lock (_lock)
		{
			uint id = 0x00980900;
			foreach (Spec s in _specs)
			{
				if (s.Name == "auto_exposure") text.Append("\nCamera Controls\n\n");
				string type = s.Type switch { CameraControlType.Boolean => "bool", CameraControlType.Menu => "menu", _ => "int" };
				string range = s.Type switch
				{
					CameraControlType.Boolean => "",
					CameraControlType.Menu => $"min={s.Min} max={s.Max} ",
					_ => $"min={s.Min} max={s.Max} step={s.Step} "
				};
				long value = _values[s.Name];
				string label = s.Menu?.FirstOrDefault(m => m.Item1 == value).Item2 is { } l ? $" ({l})" : "";
				string flags = Inactive(s) ? " flags=inactive" : "";
				text.Append(CultureInfo.InvariantCulture, $"{s.Name,31} 0x{id++:x8} ({type}){new string(' ', 7 - type.Length)}: {range}default={s.Default} value={value}{label}{flags}\n");
				foreach ((long v, string name) in s.Menu ?? [])
					text.Append(CultureInfo.InvariantCulture, $"\t\t\t\t{v}: {name}\n");
			}
		}
		return text.ToString();
	}

	private bool Inactive(Spec s)
	{
		if (s.ActiveWhen is null) return false;
		string[] rule = s.ActiveWhen.Split('=');
		return _values[rule[0]] != long.Parse(rule[1], CultureInfo.InvariantCulture);
	}
}
