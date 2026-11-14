namespace PrintDeck.Domain.Settings;

public sealed record PrinterProfile
{
	public string Name { get; init; } = "My Printer";
	public double BedWidth { get; init; } = 220;
	public double BedDepth { get; init; } = 220;
	public double MaxHeight { get; init; } = 250;
	public int ExtruderCount { get; init; } = 1;
	public bool HasHeatedBed { get; init; } = true;
	public int DefaultBaudRate { get; init; } = 115200;
	public string? LastPort { get; init; }
	public int JogFeedrateXY { get; init; } = 6000;
	public int JogFeedrateZ { get; init; } = 600;
	public int ExtrudeFeedrate { get; init; } = 300;
	public string? WebcamStreamUrl { get; init; }
	public string ApiKey { get; init; } = "";
	public string CancelGcode { get; init; } = "M104 S0\nM140 S0\nM107\nG91\nG1 Z10 F600\nG90\nM84";
	public IReadOnlyList<TemperaturePreset> Presets { get; init; } =
	[
		new("PLA", 205, 60),
		new("PETG", 240, 80),
		new("ABS", 250, 100)
	];
}
