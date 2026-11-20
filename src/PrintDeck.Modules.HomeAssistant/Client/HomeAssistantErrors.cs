namespace PrintDeck.Modules.HomeAssistant.Client;

public static class HomeAssistantErrors
{
	public const string NoAnswer = "Home Assistant did not answer.";
	public const string RefusedToken = "Home Assistant refused the token.";
	public const string NoUrl = "Add the Home Assistant URL first.";
	public const string NoToken = "Add a long-lived access token first.";
	public const string NoLight = "Choose the printer light in Settings → Modules first.";
	public const string Off = "The Home Assistant module is switched off.";

	public static string NoEntity(string entityId) => $"Home Assistant has no {entityId}.";
	public static string Answered(int status) => $"Home Assistant answered {status}.";
}
