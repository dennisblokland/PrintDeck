namespace PrintDeck.Web.Auth;

public static class AuthSchemes
{
	/// <summary>Default scheme: the API key when the request carries one, otherwise the Identity login cookie.</summary>
	public const string Auto = "PrintDeck";
	public const string ApiKey = "ApiKey";

	/// <summary>Authorization policy for the OctoPrint-compatible slicer API: API key only.</summary>
	public const string ApiKeyPolicy = "ApiKey";
}
