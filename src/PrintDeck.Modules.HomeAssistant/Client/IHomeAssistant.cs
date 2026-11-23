using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Client;

/// <summary>The part of Home Assistant's REST API the module uses. Failures are results with a sentence for the user.</summary>
public interface IHomeAssistant
{
	Task<PluginResult<EntityState>> GetStateAsync(string entityId, CancellationToken ct);

	/// <summary>Every entity; the settings use it to offer the lights and switches.</summary>
	Task<PluginResult<IReadOnlyList<EntityState>>> ListStatesAsync(CancellationToken ct);

	/// <summary><c>POST /api/services/&lt;domain&gt;/&lt;service&gt;</c> for one entity, e.g. <c>light</c> / <c>turn_on</c>.</summary>
	Task<PluginResult> CallServiceAsync(string domain, string service, string entityId, CancellationToken ct);
}
