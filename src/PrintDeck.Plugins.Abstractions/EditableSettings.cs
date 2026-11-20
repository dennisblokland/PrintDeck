namespace PrintDeck.Plugins;

/// <param name="Settings">The saved settings with every secret empty.</param>
/// <param name="SavedSecrets">Names of the secret properties that have a saved value.</param>
public sealed record EditableSettings<T>(T Settings, IReadOnlySet<string> SavedSecrets) where T : class
{
	public bool IsSaved(string property) => SavedSecrets.Contains(property);
}
