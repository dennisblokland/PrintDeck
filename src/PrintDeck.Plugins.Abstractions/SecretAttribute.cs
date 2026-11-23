namespace PrintDeck.Plugins;

/// <summary>
/// Marks a settings property as a secret (a token, a password): PrintDeck never sends it to a page again after it is
/// saved, and an empty value on save keeps the saved one. It is kept in <c>settings.json</c> and in backups, like the API key.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SecretAttribute : Attribute;
