using System.Globalization;

namespace PrintDeck.Application.Files;

public static partial class GcodeAnalyzer
{
	/// <summary>Very rough kinematics: distance / feedrate, no acceleration.</summary>
	private sealed class MoveSimulator
	{
		private double _x, _y, _z, _e, _feed = 1500;
		private bool _relative, _relativeE;
		public double Seconds { get; private set; }
		public double Extruded { get; private set; }

		public void Apply(string line)
		{
			string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0) return;
			switch (parts[0].ToUpperInvariant())
			{
				case "G90": _relative = false; _relativeE = false; break;
				case "G91": _relative = true; _relativeE = true; break;
				case "M82": _relativeE = false; break;
				case "M83": _relativeE = true; break;
				case "G92":
					foreach (string? p in parts.Skip(1))
						if (p[0] is 'E' or 'e' && Num(p) is { } reset) _e = reset;
					break;
				case "G0" or "G1":
					double x = _x, y = _y, z = _z, e = _e;
					foreach (string? p in parts.Skip(1))
					{
						if (Num(p) is not { } v) continue;
						switch (char.ToUpperInvariant(p[0]))
						{
							case 'X': x = _relative ? _x + v : v; break;
							case 'Y': y = _relative ? _y + v : v; break;
							case 'Z': z = _relative ? _z + v : v; break;
							case 'E': e = _relativeE ? _e + v : v; break;
							case 'F': if (v > 0) _feed = v; break;
						}
					}
					double dist = Math.Sqrt(Math.Pow(x - _x, 2) + Math.Pow(y - _y, 2) + Math.Pow(z - _z, 2));
					double de = e - _e;
					if (de > 0) Extruded += de;
					Seconds += Math.Max(dist, Math.Abs(de)) / (_feed / 60.0);
					(_x, _y, _z, _e) = (x, y, z, e);
					break;
			}
		}

		private static double? Num(string p) =>
			double.TryParse(p.AsSpan(1), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;
	}
}
