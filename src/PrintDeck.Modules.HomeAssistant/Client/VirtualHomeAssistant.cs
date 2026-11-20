using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Client;

/// <summary>
/// An in-process Home Assistant (<see cref="HomeAssistantSettings.Virtual"/> as the URL) with a printer light and a
/// power switch, so the module can be tried and tested without a smart home. Any token is accepted.
/// </summary>
public sealed class VirtualHomeAssistant : IHomeAssistant
{
	public const string Light = "light.printer";
	public const string Power = "switch.printer_power";

	private readonly Lock _gate = new();
	private readonly Dictionary<string, EntityState> _entities = new(StringComparer.Ordinal)
	{
		[Light] = new EntityState(Light, "off", "Printer light"),
		[Power] = new EntityState(Power, "on", "Printer power")
	};

	/// <summary>Service calls in order, e.g. "light.turn_on light.printer" (for tests).</summary>
	public IReadOnlyList<string> Calls
	{
		get { lock (_gate) return [.. _calls]; }
	}

	private readonly List<string> _calls = [];

	public Task<PluginResult<EntityState>> GetStateAsync(string entityId, CancellationToken ct)
	{
		lock (_gate)
			return Task.FromResult(_entities.TryGetValue(entityId, out EntityState? state)
				? PluginResult.Success(state)
				: PluginResult.Failure<EntityState>(HomeAssistantErrors.NoEntity(entityId)));
	}

	public Task<PluginResult<IReadOnlyList<EntityState>>> ListStatesAsync(CancellationToken ct)
	{
		lock (_gate) return Task.FromResult(PluginResult.Success<IReadOnlyList<EntityState>>([.. _entities.Values]));
	}

	public Task<PluginResult> CallServiceAsync(string domain, string service, string entityId, CancellationToken ct)
	{
		lock (_gate)
		{
			if (!_entities.TryGetValue(entityId, out EntityState? state) || EntityId.Domain(entityId) != domain)
				return Task.FromResult(PluginResult.Failure(HomeAssistantErrors.NoEntity(entityId)));
			string? next = service switch
			{
				"turn_on" => "on",
				"turn_off" => "off",
				"toggle" => state.IsOn ? "off" : "on",
				_ => null
			};
			if (next is null) return Task.FromResult(PluginResult.Failure(HomeAssistantErrors.Answered(400)));
			_entities[entityId] = state with { State = next };
			_calls.Add($"{domain}.{service} {entityId}");
			return Task.FromResult(PluginResult.Success());
		}
	}
}
