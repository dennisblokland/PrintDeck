namespace PrintDeck.Infrastructure.Storage;

public sealed class StorageOptions
{
	public const string Section = "Storage";

	/// <summary>Root for uploads and settings. Defaults to ~/.printdeck.</summary>
	public string DataDirectory { get; set; } =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".printdeck");

	public string UploadsDirectory => Path.Combine(DataDirectory, "uploads");
	public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
	public string DatabaseFile => Path.Combine(DataDirectory, "printdeck.db");
	/// <summary>Data Protection keys: they sign login cookies, so keeping them means a restart doesn't log everyone out.</summary>
	public string KeysDirectory => Path.Combine(DataDirectory, "keys");

	/// <summary>printdeck.log and, when switched on, serial.log.</summary>
	public string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
