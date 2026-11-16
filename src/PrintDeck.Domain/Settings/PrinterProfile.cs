namespace PrintDeck.Domain.Settings;

/// <summary>One printer's hardware: build volume, extruders, heated bed and manual-motion speeds.</summary>
public sealed record PrinterProfile
{
	public const string DefaultId = "default";

	public string Id { get; init; } = DefaultId;
	public string Name { get; init; } = "My Printer";
	public double BedWidth { get; init; } = 220;
	public double BedDepth { get; init; } = 220;
	public double MaxHeight { get; init; } = 250;
	public int ExtruderCount { get; init; } = 1;
	public bool HasHeatedBed { get; init; } = true;
	public int JogFeedrateXY { get; init; } = 6000;
	public int JogFeedrateZ { get; init; } = 600;
	public int ExtrudeFeedrate { get; init; } = 300;

	public static string NewId() => Guid.NewGuid().ToString("N")[..12];
}
