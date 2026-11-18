namespace PrintDeck.Web.Services;

/// <summary>
/// A stored file's path for use in a URL: each folder and the name escaped, the '/' between them kept,
/// so catch-all routes such as <c>/thumbnail/{**name}</c> get the path back.
/// </summary>
public static class FileUrl
{
	public static string Path(string name) => string.Join('/', name.Split('/').Select(Uri.EscapeDataString));
}
