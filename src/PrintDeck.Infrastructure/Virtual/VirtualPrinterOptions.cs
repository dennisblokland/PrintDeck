namespace PrintDeck.Infrastructure.Virtual;

/// <summary>The <c>VirtualPrinter</c> section of appsettings.json.</summary>
public sealed class VirtualPrinterOptions
{
	public const string Section = "VirtualPrinter";
	public const string DefaultFirmwareName = "Marlin 2.1.2 (PrintDeck Virtual)";

	/// <summary>What the virtual printer reports in M115, e.g. "Marlin 1.0.2" to try the firmware health check.</summary>
	public string FirmwareName { get; set; } = DefaultFirmwareName;
}
