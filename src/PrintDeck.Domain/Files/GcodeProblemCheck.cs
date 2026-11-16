using System.Globalization;
using System.Text.RegularExpressions;

using PrintDeck.Domain.Files.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Domain.Files;

/// <summary>
/// Reads a file line by line before it is printed and collects known problems: binary G-code, slicer placeholders
/// that were never filled in, commands that break a streamed print, lines too long for Marlin, extrusion outside the
/// printer profile's build volume, extruding without a hotend temperature, and files that never move.
/// </summary>
public sealed partial class GcodeProblemCheck(PrinterProfile profile)
{
	/// <summary>Marlin's MAX_CMD_SIZE is 96 including "N12345 " and "*123"; leave room for those.</summary>
	public const int MaxCommandLength = 80;

	/// <summary>Purge lines and wipes legitimately extrude just off the bed (Prusa MK3S at Y -3).</summary>
	public const double EdgeMargin = 10;

	private readonly Dictionary<(GcodeProblemKind Kind, string Key), (GcodeProblem Problem, int Count)> _found = [];
	private long _line;
	private bool _binary;
	private bool _hotendSet;
	private bool _moved;
	private bool _relative, _relativeE;
	private double _x, _y, _z, _e;
	private double _minX = double.MaxValue, _minY = double.MaxValue, _maxX = double.MinValue, _maxY = double.MinValue, _maxZ = double.MinValue;
	private long _firstOutside;

	[GeneratedRegex(@"\{\s*[A-Za-z_][^{}]*\}|\[[A-Za-z_][A-Za-z0-9_]*\]")]
	private static partial Regex Placeholder();

	public void Inspect(string raw)
	{
		_line++;
		if (_binary) return;
		if (_line == 1 && raw.StartsWith("GCDE", StringComparison.Ordinal) || raw.Contains('\0'))
		{
			_binary = true;
			Add(GcodeProblemKind.BinaryFile, GcodeProblemSeverity.Error, "",
				"This is binary G-code (.bgcode). PrintDeck streams plain text: slice again with binary G-code switched off.", null);
			return;
		}

		int semicolon = raw.IndexOf(';');
		string command = (semicolon >= 0 ? raw[..semicolon] : raw).Trim();
		if (command.Length == 0) return;

		if (Placeholder().Match(command) is { Success: true } placeholder)
			Add(GcodeProblemKind.UnreplacedPlaceholder, GcodeProblemSeverity.Error, placeholder.Value,
				$"The slicer left {placeholder.Value} unfilled; the printer reads it as 0.", _line);

		if (command.Length > MaxCommandLength)
			Add(GcodeProblemKind.LineTooLong, GcodeProblemSeverity.Warning, "",
				$"Lines over {MaxCommandLength} characters get cut off by Marlin.", _line);

		string[] words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		string code = words[0].ToUpperInvariant();
		switch (code)
		{
			case "M112":
				Add(GcodeProblemKind.BreaksStreaming, GcodeProblemSeverity.Error, code, "M112 is an emergency stop: the printer halts mid-print.", _line);
				break;
			case "M110":
				Add(GcodeProblemKind.BreaksStreaming, GcodeProblemSeverity.Error, code, "M110 resets the line numbers PrintDeck is counting; the print stalls on resends.", _line);
				break;
			case "M28" or "M29":
				Add(GcodeProblemKind.BreaksStreaming, GcodeProblemSeverity.Error, "M28", $"{code} writes to the SD card: the rest of the file would be stored instead of printed.", _line);
				break;
			case "M997":
				Add(GcodeProblemKind.BreaksStreaming, GcodeProblemSeverity.Error, code, "M997 starts a firmware update.", _line);
				break;
			case "M104" or "M109":
				if (Value(words, 'S') is > 0 || Value(words, 'R') is > 0) _hotendSet = true;
				break;
			case "G90": _relative = false; _relativeE = false; break;
			case "G91": _relative = true; _relativeE = true; break;
			case "M82": _relativeE = false; break;
			case "M83": _relativeE = true; break;
			case "G92":
				if (Value(words, 'E') is { } e0) _e = e0;
				break;
			case "G28":
				_moved = true;
				break;
			case "G0" or "G1" or "G2" or "G3":
				Move(words);
				break;
		}
	}

	private void Move(string[] words)
	{
		double x = Value(words, 'X') is { } vx ? (_relative ? _x + vx : vx) : _x;
		double y = Value(words, 'Y') is { } vy ? (_relative ? _y + vy : vy) : _y;
		double z = Value(words, 'Z') is { } vz ? (_relative ? _z + vz : vz) : _z;
		double e = Value(words, 'E') is { } ve ? (_relativeE ? _e + ve : ve) : _e;
		if (x != _x || y != _y || z != _z) _moved = true;

		if (e > _e && (x != _x || y != _y))
		{
			if (!_hotendSet)
				Add(GcodeProblemKind.NoHotendTemperature, GcodeProblemSeverity.Warning, "",
					"Extrudes before setting a hotend temperature (M104/M109); fine only if you preheat by hand.", _line);

			foreach ((double px, double py) in new[] { (_x, _y), (x, y) })
			{
				_minX = Math.Min(_minX, px); _maxX = Math.Max(_maxX, px);
				_minY = Math.Min(_minY, py); _maxY = Math.Max(_maxY, py);
			}
			_maxZ = Math.Max(_maxZ, z);
			if (_firstOutside == 0 && Outside(x, y, z)) _firstOutside = _line;
		}
		(_x, _y, _z, _e) = (x, y, z, e);
	}

	private bool Outside(double x, double y, double z) =>
		x < -EdgeMargin || y < -EdgeMargin || x > profile.BedWidth + EdgeMargin || y > profile.BedDepth + EdgeMargin || z > profile.MaxHeight;

	/// <summary>Everything found, errors first, in the order they first appear.</summary>
	public IReadOnlyList<GcodeProblem> Finish()
	{
		if (!_binary)
		{
			if (_firstOutside > 0)
				Add(GcodeProblemKind.OutsideBuildVolume, GcodeProblemSeverity.Warning, "",
					string.Create(CultureInfo.InvariantCulture,
						$"Prints at X {_minX:0}–{_maxX:0}, Y {_minY:0}–{_maxY:0}{(_maxZ > 0 ? $", Z up to {_maxZ:0.#}" : "")} mm; {profile.Name} is {profile.BedWidth:0} × {profile.BedDepth:0} × {profile.MaxHeight:0} mm. Sliced for another printer?"),
					_firstOutside);
			if (!_moved)
				Add(GcodeProblemKind.NoMoves, GcodeProblemSeverity.Warning, "", "The file never moves the printer.", null);
		}

		return _found.Values
			.Select(f => f.Problem with { Count = f.Count })
			.OrderByDescending(p => p.Severity)
			.ThenBy(p => p.FirstLine ?? 0)
			.ToList();
	}

	private void Add(GcodeProblemKind kind, GcodeProblemSeverity severity, string key, string message, long? line)
	{
		_found[(kind, key)] = _found.TryGetValue((kind, key), out var seen)
			? (seen.Problem, seen.Count + 1)
			: (new GcodeProblem(kind, severity, message, line), 1);
	}

	private static double? Value(string[] words, char letter)
	{
		foreach (string w in words.AsSpan(1))
			if (w.Length > 1 && char.ToUpperInvariant(w[0]) == letter
				&& double.TryParse(w.AsSpan(1), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
				return v;
		return null;
	}
}
