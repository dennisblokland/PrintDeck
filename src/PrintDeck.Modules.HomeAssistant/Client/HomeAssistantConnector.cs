using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Client;

/// <summary>The Home Assistant the settings point at: the virtual one, or the REST API with the token.</summary>
public sealed class HomeAssistantConnector(VirtualHomeAssistant virtualHomeAssistant) : IDisposable
{
	private readonly HttpClient _http = new();

	public PluginResult<IHomeAssistant> For(HomeAssistantSettings settings)
	{
		if (settings.IsVirtual) return PluginResult.Success<IHomeAssistant>(virtualHomeAssistant);
		if (settings.Url.Length == 0) return PluginResult.Failure<IHomeAssistant>(HomeAssistantErrors.NoUrl);
		if (settings.Token.Length == 0) return PluginResult.Failure<IHomeAssistant>(HomeAssistantErrors.NoToken);
		if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out Uri? url)) return PluginResult.Failure<IHomeAssistant>(HomeAssistantErrors.NoUrl);
		return PluginResult.Success<IHomeAssistant>(new HttpHomeAssistant(_http, url, settings.Token));
	}

	public void Dispose() => _http.Dispose();
}
