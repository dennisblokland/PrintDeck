using System.Globalization;

namespace PrintDeck.Domain.Jobs;

/// <summary>
/// Follows the slicer's layer markers in a G-code file as it is streamed: Cura and OrcaSlicer write
/// <c>;LAYER:0</c>, <c>;LAYER:1</c>…, PrusaSlicer only <c>;LAYER_CHANGE</c>.
/// </summary>
public sealed class LayerCounter
{
	private bool _explicitLayers;

	/// <summary>Current layer (1-based), once the file has marked one; 0 before that.</summary>
	public int Layer { get; private set; }

	/// <summary>Looks at one raw line; returns the new layer when this line starts one, otherwise null.</summary>
	public int? OnLine(string raw)
	{
		string line = raw.Trim();
		if (!line.StartsWith(';')) return null;

		string comment = line[1..].TrimStart();
		int? layer = null;
		// Cura and OrcaSlicer number layers from 0 ("LAYER:0"); PrusaSlicer only marks the change.
		if (comment.StartsWith("LAYER:", StringComparison.Ordinal)
			&& int.TryParse(comment.AsSpan(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
		{
			_explicitLayers = true;
			layer = n + 1;
		}
		else if (!_explicitLayers && comment.StartsWith("LAYER_CHANGE", StringComparison.Ordinal))
			layer = Layer + 1;

		if (layer is not { } l || l == Layer) return null;
		Layer = l;
		return l;
	}
}
