namespace PrintDeck.Web.Auth;

public static class LocalUrl
{
	/// <summary>
	/// The return URL if it stays on this site (a path like <c>/files</c>), otherwise "" (the dashboard).
	/// Stops <c>?returnUrl=https://evil.example</c> and <c>//evil.example</c> from turning the login page into a redirector.
	/// </summary>
	public static string OrHome(string? url) =>
		url is { Length: > 0 } && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) && !url.Contains(':', StringComparison.Ordinal)
			? url
			: "";
}
