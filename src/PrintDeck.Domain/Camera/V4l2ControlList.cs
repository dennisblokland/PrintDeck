using System.Globalization;
using System.Text.RegularExpressions;

using PrintDeck.Domain.Camera.Enums;

namespace PrintDeck.Domain.Camera;

/// <summary>
/// Reads the output of <c>v4l2-ctl --list-ctrls-menus</c>:
/// <code>
///                      brightness 0x00980900 (int)    : min=-64 max=64 step=1 default=0 value=0
///                   auto_exposure 0x009a0901 (menu)   : min=0 max=3 default=3 value=3 (Aperture Priority Mode)
/// 				1: Manual Mode
/// 				3: Aperture Priority Mode
/// </code>
/// Controls of other types (button, string, bitmask) and disabled ones are skipped.
/// </summary>
public static partial class V4l2ControlList
{
	[GeneratedRegex(@"^\s*(?<name>[a-z0-9_]+)\s+0x[0-9a-f]+\s+\((?<type>[a-z0-9]+)\)\s*:(?<rest>.*)$")]
	private static partial Regex ControlLine();

	[GeneratedRegex(@"^\s+(?<value>-?\d+):\s*(?<label>.*?)\s*$")]
	private static partial Regex MenuLine();

	[GeneratedRegex(@"(?<key>[a-z_]+)=(?<value>\S+)")]
	private static partial Regex Field();

	[GeneratedRegex("^[a-z0-9_]+$")]
	private static partial Regex ValidName();

	/// <summary>Whether <paramref name="name"/> looks like a v4l2 control name, so it is safe to pass to v4l2-ctl.</summary>
	public static bool IsValidName(string name) => ValidName().IsMatch(name);

	public static IReadOnlyList<CameraControl> Parse(string output)
	{
		List<CameraControl> controls = [];
		CameraControl? current = null;
		List<CameraMenuItem> menu = [];

		void Finish()
		{
			if (current is not null) controls.Add(current with { Menu = current.Type == CameraControlType.Menu ? menu.ToList() : [] });
			current = null;
			menu.Clear();
		}

		foreach (string line in output.Replace("\r", "").Split('\n'))
		{
			if (ControlLine().Match(line) is { Success: true } control)
			{
				Finish();
				current = Read(control.Groups["name"].Value, control.Groups["type"].Value, control.Groups["rest"].Value);
			}
			else if (current is { Type: CameraControlType.Menu } && MenuLine().Match(line) is { Success: true } item
					 && long.TryParse(item.Groups["value"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
			{
				menu.Add(new CameraMenuItem(value, item.Groups["label"].Value));
			}
			else if (line.Trim().Length > 0 && !char.IsWhiteSpace(line[0]))
			{
				Finish(); // a section title such as "Camera Controls"
			}
		}
		Finish();
		return controls;
	}

	private static CameraControl? Read(string name, string type, string rest)
	{
		CameraControlType? kind = type switch
		{
			"int" or "int64" => CameraControlType.Integer,
			"bool" => CameraControlType.Boolean,
			"menu" or "intmenu" => CameraControlType.Menu,
			_ => null
		};
		if (kind is not { } k) return null;

		Dictionary<string, string> fields = Field().Matches(rest).ToDictionary(m => m.Groups["key"].Value, m => m.Groups["value"].Value);
		string[] flags = fields.TryGetValue("flags", out string? f) ? f.Split(',') : [];
		if (flags.Contains("disabled")) return null;

		long Number(string key, long fallback) =>
			fields.TryGetValue(key, out string? s) && long.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long n) ? n : fallback;

		bool isBool = k == CameraControlType.Boolean;
		return new CameraControl(
			name, k,
			Min: isBool ? 0 : Number("min", 0),
			Max: isBool ? 1 : Number("max", 0),
			Step: isBool ? 1 : Math.Max(1, Number("step", 1)),
			Default: Number("default", 0),
			Value: Number("value", 0),
			Inactive: flags.Contains("inactive") || flags.Contains("read-only") || flags.Contains("grabbed"),
			Menu: []);
	}
}
