namespace PrintDeck.Domain.Users;

/// <summary>
/// An app asking for its own key through OctoPrint's app-key flow (Home Assistant does this when it is set up).
/// An admin allows or denies it in PrintDeck; the key then goes to the app once, when it asks again.
/// </summary>
/// <param name="Token">What the app polls with; never the key itself.</param>
/// <param name="App">The name the app gives, e.g. "Home Assistant"; the key is named after it.</param>
/// <param name="User">The account the app asks to act as, if it named one.</param>
public sealed record AppKeyRequest(string Token, string App, string? User, DateTimeOffset RequestedAt)
{
	public const int MaxNameLength = 64;
}
