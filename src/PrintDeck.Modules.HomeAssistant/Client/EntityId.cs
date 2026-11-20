namespace PrintDeck.Modules.HomeAssistant.Client;

public static class EntityId
{
	/// <summary>The domains whose entities the module switches: lights and plain switches (smart plugs).</summary>
	public static readonly IReadOnlyList<string> SwitchableDomains = ["light", "switch"];

	/// <summary>"light" for <c>light.printer</c>.</summary>
	public static string Domain(string entityId) => entityId.Split('.', 2)[0];

	public static bool IsSwitchable(string entityId) =>
		entityId.Split('.', 2) is [{ } domain, { Length: > 0 }] && SwitchableDomains.Contains(domain);
}
