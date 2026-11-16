namespace PrintDeck.Domain.Jobs.Enums;

public enum JobKind
{
	/// <summary>PrintDeck streams the file line by line.</summary>
	Print,

	/// <summary>The printer prints a file from its own SD card; PrintDeck follows its progress.</summary>
	SdPrint,

	/// <summary>PrintDeck copies a file onto the SD card (<c>M28</c>/<c>M29</c>); nothing moves.</summary>
	SdUpload
}
