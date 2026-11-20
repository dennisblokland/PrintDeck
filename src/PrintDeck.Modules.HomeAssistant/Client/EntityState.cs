namespace PrintDeck.Modules.HomeAssistant.Client;

/// <summary>One entity as Home Assistant reports it (<c>GET /api/states</c>).</summary>
/// <param name="State">"on", "off", "unavailable"…</param>
public sealed record EntityState(string EntityId, string State, string? FriendlyName = null)
{
	public bool IsOn => State == "on";
	public string DisplayName => string.IsNullOrWhiteSpace(FriendlyName) ? EntityId : $"{FriendlyName} ({EntityId})";
}
