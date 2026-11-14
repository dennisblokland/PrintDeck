namespace PrintDeck.Infrastructure.Storage;

public sealed class StorageOptions
{
	public const string Section = "Storage";

	/// <summary>Root for uploads and settings. Defaults to ~/.printdeck.</summary>
	public string DataDirectory { get; set; } =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".printdeck");

	public string UploadsDirectory => Path.Combine(DataDirectory, "uploads");
	public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
