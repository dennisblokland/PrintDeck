using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Modules.HomeAssistant.Light.Enums;
using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Light;

/// <summary>
/// Switches the printer light through Home Assistant. Works while the module runs (<see cref="Attach"/>): it reads the
/// settings, token included, through the module's own context. Each switch is logged to the terminal.
/// </summary>
public sealed class PrinterLight(HomeAssistantConnector connector)
{
	public const string AdminsOnly = "Only admins can do that.";

	private IPluginContext? _module;

	/// <summary>The light was switched, by <see cref="LightSource"/>. Raised on the caller's thread.</summary>
	public event Action<LightSource>? Changed;

	public void Attach(IPluginContext module) => Volatile.Write(ref _module, module);

	public void Detach() => Volatile.Write(ref _module, null);

	/// <summary>Whether the light is on; fails when the module is off or Home Assistant can't be reached.</summary>
	public async Task<PluginResult<bool>> GetStateAsync(CancellationToken ct = default)
	{
		PluginResult<(HomeAssistantSettings Settings, IHomeAssistant Client)> ha = await ConnectAsync(ct);
		if (!ha.IsSuccess) return PluginResult.Failure<bool>(ha.Error!);
		PluginResult<EntityState> state = await ha.Value.Client.GetStateAsync(ha.Value.Settings.LightEntity, ct);
		return state.IsSuccess ? PluginResult.Success(state.Value!.IsOn) : PluginResult.Failure<bool>(state.Error!);
	}

	/// <summary>Switches the light for <paramref name="caller"/>, which must act for an admin or the module itself.</summary>
	public async Task<PluginResult> SetAsync(bool on, IPluginContext caller, LightSource source, CancellationToken ct = default)
	{
		if (await caller.GetUserAsync() is not { IsAdmin: true }) return PluginResult.Failure(AdminsOnly);
		PluginResult<(HomeAssistantSettings Settings, IHomeAssistant Client)> ha = await ConnectAsync(ct);
		if (!ha.IsSuccess) return ha;

		string entity = ha.Value.Settings.LightEntity;
		PluginResult switched = await ha.Value.Client.CallServiceAsync(EntityId.Domain(entity), on ? "turn_on" : "turn_off", entity, ct);
		if (!switched.IsSuccess) return switched;

		if (Volatile.Read(ref _module) is { } module)
			await module.LogAsync(on ? "Light on through Home Assistant" : "Light off through Home Assistant", ct);
		Changed?.Invoke(source);
		return switched;
	}

	/// <summary>
	/// Checks <paramref name="candidate"/> (as typed in the settings, the saved token used when its token is empty) and
	/// lists the lights and switches Home Assistant has.
	/// </summary>
	public async Task<PluginResult<IReadOnlyList<EntityState>>> TestAsync(HomeAssistantSettings candidate, IPluginContext caller, CancellationToken ct = default)
	{
		if (await caller.GetUserAsync() is not { IsAdmin: true }) return PluginResult.Failure<IReadOnlyList<EntityState>>(AdminsOnly);
		candidate = candidate.Trimmed();
		if (candidate.Token.Length == 0 && !candidate.IsVirtual)
		{
			if (Volatile.Read(ref _module) is not { } module) return PluginResult.Failure<IReadOnlyList<EntityState>>(HomeAssistantErrors.Off);
			PluginResult<HomeAssistantSettings> saved = await module.GetSettingsAsync<HomeAssistantSettings>(ct);
			if (!saved.IsSuccess) return PluginResult.Failure<IReadOnlyList<EntityState>>(saved.Error!);
			candidate.Token = saved.Value!.Token;
		}

		PluginResult<IHomeAssistant> client = connector.For(candidate);
		if (!client.IsSuccess) return PluginResult.Failure<IReadOnlyList<EntityState>>(client.Error!);
		PluginResult<IReadOnlyList<EntityState>> states = await client.Value!.ListStatesAsync(ct);
		if (!states.IsSuccess) return states;
		return PluginResult.Success<IReadOnlyList<EntityState>>(states.Value!
			.Where(s => EntityId.IsSwitchable(s.EntityId))
			.OrderBy(s => s.EntityId, StringComparer.Ordinal)
			.ToList());
	}

	private async Task<PluginResult<(HomeAssistantSettings, IHomeAssistant)>> ConnectAsync(CancellationToken ct)
	{
		if (Volatile.Read(ref _module) is not { } module) return PluginResult.Failure<(HomeAssistantSettings, IHomeAssistant)>(HomeAssistantErrors.Off);
		PluginResult<HomeAssistantSettings> settings = await module.GetSettingsAsync<HomeAssistantSettings>(ct);
		if (!settings.IsSuccess) return PluginResult.Failure<(HomeAssistantSettings, IHomeAssistant)>(settings.Error!);
		if (!settings.Value!.HasLight) return PluginResult.Failure<(HomeAssistantSettings, IHomeAssistant)>(HomeAssistantErrors.NoLight);
		PluginResult<IHomeAssistant> client = connector.For(settings.Value);
		return client.IsSuccess
			? PluginResult.Success((settings.Value, client.Value!))
			: PluginResult.Failure<(HomeAssistantSettings, IHomeAssistant)>(client.Error!);
	}
}
