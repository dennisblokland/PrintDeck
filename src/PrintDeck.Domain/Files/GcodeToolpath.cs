namespace PrintDeck.Domain.Files;

/// <summary>What an extrusion move prints. <see cref="Travel"/> marks a point the nozzle moves to without printing.</summary>
public enum ToolpathKind : byte
{
	Travel = 0,
	OuterWall = 1,
	InnerWall = 2,
	Infill = 3,
	SolidInfill = 4,
	Support = 5,
	SkirtBrim = 6,
	Other = 7
}

/// <summary>One layer: points <c>[Start, Start + Count)</c> of the toolpath.</summary>
public readonly record struct ToolpathLayer(float Z, int Start, int Count);

/// <summary>
/// Extrusion paths of a G-code file, grouped by layer. Points are stored as parallel arrays:
/// XY in millimetres, the file byte offset at which the move has been read (matches
/// <see cref="Jobs.PrintJob.BytesProcessed"/>), and the kind of move that ends at the point.
/// </summary>
public sealed class GcodeToolpath(
	IReadOnlyList<ToolpathLayer> layers,
	float[] xy,
	uint[] offsets,
	byte[] kinds,
	(float MinX, float MinY, float MaxX, float MaxY) bounds,
	long totalBytes)
{
	public IReadOnlyList<ToolpathLayer> Layers { get; } = layers;
	public float[] Xy { get; } = xy;
	public uint[] Offsets { get; } = offsets;
	public byte[] Kinds { get; } = kinds;
	public (float MinX, float MinY, float MaxX, float MaxY) Bounds { get; } = bounds;
	public long TotalBytes { get; } = totalBytes;
	public int PointCount => Kinds.Length;
}
