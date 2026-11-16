namespace PrintDeck.Domain.Files.Enums;

public enum GcodeProblemSeverity
{
	/// <summary>Worth a look; the print may still be fine.</summary>
	Warning,

	/// <summary>The print will almost certainly go wrong; unattended starts refuse it.</summary>
	Error
}
