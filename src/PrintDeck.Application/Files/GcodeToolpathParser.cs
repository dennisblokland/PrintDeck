using System.Globalization;
using System.Text;

using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Files;

/// <summary>
/// Turns G-code into per-layer extrusion paths for the viewer. Layers follow slicer layer-change
/// comments when the file has them, otherwise Z changes on extruding moves. Byte offsets are counted
/// exactly like the print session counts them, so a job's progress maps onto the toolpath.
/// </summary>
public static class GcodeToolpathParser
{
	private const double ArcSegmentMm = 0.5;

	public static async Task<GcodeToolpath> ParseAsync(Stream stream, CancellationToken ct)
	{
		using StreamReader reader = new(stream, Encoding.UTF8);
		Builder b = new();
		long bytes = 0;
		int lineNo = 0;

		while (await reader.ReadLineAsync(ct) is { } raw)
		{
			bytes += Encoding.UTF8.GetByteCount(raw) + 1;
			if ((++lineNo & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
			b.Apply(raw, (uint) Math.Min(bytes, uint.MaxValue));
		}

		return b.Build(bytes);
	}

	internal static ToolpathKind KindOf(string type)
	{
		string t = type.Trim().ToLowerInvariant();
		if (t.Contains("outer") || t.Contains("external")) return ToolpathKind.OuterWall;
		if (t.Contains("support")) return ToolpathKind.Support;
		if (t.Contains("skirt") || t.Contains("brim")) return ToolpathKind.SkirtBrim;
		if (t.Contains("wall") || t.Contains("perimeter")) return ToolpathKind.InnerWall;
		if (t.Contains("solid") || t.Contains("top") || t.Contains("bottom") || t.Contains("skin")
			|| t.Contains("bridge") || t.Contains("ironing")) return ToolpathKind.SolidInfill;
		if (t.Contains("infill") || t == "fill") return ToolpathKind.Infill;
		return ToolpathKind.Other;
	}

	private sealed class Builder
	{
		private double _x, _y, _z, _e;
		private bool _relative, _relativeE;
		private ToolpathKind _kind = ToolpathKind.Other;
		private bool _markers, _pendingLayer;

		private readonly List<ToolpathLayer> _layers = [];
		private float[] _xy = new float[1 << 16];
		private uint[] _offsets = new uint[1 << 15];
		private byte[] _kinds = new byte[1 << 15];
		private int _count;
		private int _layerStart = -1;
		private float _layerZ;
		private bool _layerMarked;
		private float _lastX = float.NaN, _lastY = float.NaN;
		private float _minX = float.MaxValue, _minY = float.MaxValue, _maxX = float.MinValue, _maxY = float.MinValue;

		public void Apply(string raw, uint offset)
		{
			ReadOnlySpan<char> line = raw.AsSpan().Trim();
			if (line.IsEmpty) return;

			int semi = line.IndexOf(';');
			if (semi >= 0)
			{
				Comment(line[(semi + 1)..].TrimStart());
				line = line[..semi].TrimEnd();
				if (line.IsEmpty) return;
			}

			Word cmd = default;
			double? x = null, y = null, z = null, e = null, i = null, j = null;
			bool hasX = false, hasY = false, hasZ = false;
			bool first = true;
			foreach (Word w in Words(line))
			{
				if (first) { cmd = w; first = false; continue; }
				hasX |= w.Letter == 'X';
				hasY |= w.Letter == 'Y';
				hasZ |= w.Letter == 'Z';
				if (double.IsNaN(w.Value)) continue;
				switch (w.Letter)
				{
					case 'X': x = w.Value; break;
					case 'Y': y = w.Value; break;
					case 'Z': z = w.Value; break;
					case 'E': e = w.Value; break;
					case 'I': i = w.Value; break;
					case 'J': j = w.Value; break;
				}
			}

			switch (cmd.Letter, cmd.Value)
			{
				case ('G', 0) or ('G', 1):
					Move(x, y, z, e, offset);
					break;
				case ('G', 2) or ('G', 3):
					Arc(x, y, z, e, i ?? 0, j ?? 0, clockwise: cmd.Value == 2, offset);
					break;
				case ('G', 28):
					bool all = !hasX && !hasY && !hasZ;
					if (all || hasX) _x = 0;
					if (all || hasY) _y = 0;
					if (all || hasZ) _z = 0;
					break;
				case ('G', 90): _relative = false; _relativeE = false; break;
				case ('G', 91): _relative = true; _relativeE = true; break;
				case ('M', 82): _relativeE = false; break;
				case ('M', 83): _relativeE = true; break;
				case ('G', 92):
					if (x is { } nx) _x = nx;
					if (y is { } ny) _y = ny;
					if (z is { } nz) _z = nz;
					if (e is { } ne) _e = ne;
					break;
			}
		}

		private void Comment(ReadOnlySpan<char> c)
		{
			if (c.StartsWith("TYPE:", StringComparison.Ordinal))
				_kind = KindOf(c[5..].ToString());
			else if (c.StartsWith("FEATURE:", StringComparison.Ordinal)) // Bambu/Orca with label objects
				_kind = KindOf(c[8..].ToString());
			else if (c.StartsWith("LAYER_CHANGE", StringComparison.Ordinal) || c.StartsWith("LAYER:", StringComparison.Ordinal))
				_markers = _pendingLayer = true;
		}

		private void Move(double? x, double? y, double? z, double? e, uint offset)
		{
			double nx = x is { } vx ? (_relative ? _x + vx : vx) : _x;
			double ny = y is { } vy ? (_relative ? _y + vy : vy) : _y;
			double nz = z is { } vz ? (_relative ? _z + vz : vz) : _z;
			double ne = e is { } ve ? (_relativeE ? _e + ve : ve) : _e;

			bool extrudes = ne - _e > 1e-6 && (nx != _x || ny != _y);
			if (extrudes) Segment(_x, _y, nx, ny, nz, offset);
			(_x, _y, _z, _e) = (nx, ny, nz, ne);
		}

		private void Arc(double? x, double? y, double? z, double? e, double i, double j, bool clockwise, uint offset)
		{
			double nx = x is { } vx ? (_relative ? _x + vx : vx) : _x;
			double ny = y is { } vy ? (_relative ? _y + vy : vy) : _y;
			double nz = z is { } vz ? (_relative ? _z + vz : vz) : _z;
			double ne = e is { } ve ? (_relativeE ? _e + ve : ve) : _e;

			if (ne - _e > 1e-6)
			{
				double cx = _x + i, cy = _y + j;
				double r = Math.Sqrt(i * i + j * j);
				double a0 = Math.Atan2(_y - cy, _x - cx);
				double a1 = Math.Atan2(ny - cy, nx - cx);
				double sweep = a1 - a0;
				if (clockwise && sweep >= -1e-9) sweep -= 2 * Math.PI;
				if (!clockwise && sweep <= 1e-9) sweep += 2 * Math.PI;

				int steps = Math.Clamp((int) Math.Ceiling(Math.Abs(sweep) * r / ArcSegmentMm), 1, 720);
				double px = _x, py = _y;
				for (int s = 1; s <= steps; s++)
				{
					double a = a0 + sweep * s / steps;
					double qx = s == steps ? nx : cx + r * Math.Cos(a);
					double qy = s == steps ? ny : cy + r * Math.Sin(a);
					Segment(px, py, qx, qy, nz, offset);
					(px, py) = (qx, qy);
				}
			}
			(_x, _y, _z, _e) = (nx, ny, nz, ne);
		}

		private void Segment(double x0, double y0, double x1, double y1, double z, uint offset)
		{
			float fz = (float) z;
			bool newLayer = _layerStart < 0 || (_markers ? _pendingLayer : Math.Abs(fz - _layerZ) > 1e-4f);
			if (newLayer)
			{
				bool fromMarker = _pendingLayer;
				_pendingLayer = false;
				// The first marked layer continues an unmarked one at the same height (purge line in start G-code).
				bool continues = _layerStart >= 0 && fromMarker && !_layerMarked && Math.Abs(fz - _layerZ) <= 1e-4f;
				if (!continues)
				{
					CloseLayer();
					_layerStart = _count;
					_lastX = _lastY = float.NaN;
				}
				_layerZ = fz;
				_layerMarked = fromMarker;
			}

			float fx0 = (float) x0, fy0 = (float) y0, fx1 = (float) x1, fy1 = (float) y1;
			if (fx0 != _lastX || fy0 != _lastY) Add(fx0, fy0, offset, ToolpathKind.Travel);
			Add(fx1, fy1, offset, _kind);
			Grow(fx0, fy0);
			Grow(fx1, fy1);
		}

		private void Add(float x, float y, uint offset, ToolpathKind kind)
		{
			if (_count == _kinds.Length)
			{
				int size = _kinds.Length * 2;
				Array.Resize(ref _xy, size * 2);
				Array.Resize(ref _offsets, size);
				Array.Resize(ref _kinds, size);
			}
			_xy[_count * 2] = x;
			_xy[_count * 2 + 1] = y;
			_offsets[_count] = offset;
			_kinds[_count] = (byte) kind;
			_count++;
			(_lastX, _lastY) = (x, y);
		}

		private void Grow(float x, float y)
		{
			if (x < _minX) _minX = x;
			if (y < _minY) _minY = y;
			if (x > _maxX) _maxX = x;
			if (y > _maxY) _maxY = y;
		}

		private void CloseLayer()
		{
			if (_layerStart >= 0 && _count > _layerStart)
				_layers.Add(new ToolpathLayer(_layerZ, _layerStart, _count - _layerStart));
		}

		public GcodeToolpath Build(long totalBytes)
		{
			CloseLayer();
			Array.Resize(ref _xy, _count * 2);
			Array.Resize(ref _offsets, _count);
			Array.Resize(ref _kinds, _count);
			var bounds = _count == 0 ? (0f, 0f, 0f, 0f) : (_minX, _minY, _maxX, _maxY);
			return new GcodeToolpath(_layers, _xy, _offsets, _kinds, bounds, totalBytes);
		}
	}

	private readonly record struct Word(char Letter, double Value);

	private static List<Word> Words(ReadOnlySpan<char> line)
	{
		List<Word> words = new(6);
		int p = 0;
		while (p < line.Length)
		{
			while (p < line.Length && char.IsWhiteSpace(line[p])) p++;
			if (p >= line.Length) break;
			char letter = char.ToUpperInvariant(line[p++]);
			if (!char.IsAsciiLetter(letter)) break; // not G-code words (e.g. M117 text)
			while (p < line.Length && line[p] == ' ') p++;
			int start = p;
			if (p < line.Length && line[p] is '-' or '+') p++;
			while (p < line.Length && (char.IsAsciiDigit(line[p]) || line[p] == '.')) p++;
			// No exponent: "E" is the extruder axis, so numbers stop at it.
			if (double.TryParse(line[start..p], NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
				words.Add(new Word(letter, v));
			else if (words.Count == 0)
				break;
			else
				words.Add(new Word(letter, double.NaN)); // bare axis, e.g. "G28 X"

		}
		return words;
	}
}
