namespace PrintDeck.Domain.Files.Enums;

public enum FileSort
{
	/// <summary>Newest upload first.</summary>
	Uploaded,

	/// <summary>A to Z, ignoring case.</summary>
	Name,

	/// <summary>Most recently printed first; never printed last.</summary>
	LastPrinted,

	/// <summary>Largest first.</summary>
	Size
}
