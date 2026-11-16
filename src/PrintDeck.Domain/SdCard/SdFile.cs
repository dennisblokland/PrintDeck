namespace PrintDeck.Domain.SdCard;

/// <summary>A file on the printer's SD card, as <c>M20</c> lists it.</summary>
/// <param name="Path">DOS 8.3 path the firmware uses in <c>M23</c>/<c>M30</c>, e.g. <c>CUBE~1.GCO</c> or <c>DIR/CUBE.GCO</c>.</param>
/// <param name="SizeBytes">Size in bytes; 0 when the firmware does not report it.</param>
/// <param name="LongName">Long file name, when the firmware reports it (<c>M20 L</c>).</param>
public sealed record SdFile(string Path, long SizeBytes, string? LongName = null)
{
	public string DisplayName => string.IsNullOrWhiteSpace(LongName) ? Path : LongName;
}
