using Microsoft.AspNetCore.Components;

using PrintDeck.Modules.HomeAssistant.Light;
using PrintDeck.Modules.HomeAssistant.Light.Enums;
using PrintDeck.Plugins;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Modules.HomeAssistant.Components;

/// <summary>
/// Base of the light toggles: shown to admins once a light is chosen, with its state, refreshed when anyone switches
/// it or the settings change.
/// </summary>
public abstract class LightComponent : ComponentBase, IDisposable
{
	[Parameter, EditorRequired] public IPluginContext Context { get; set; } = default!;
	[Inject] protected PrinterLight Light { get; set; } = default!;

	protected bool Visible { get; private set; }

	/// <summary>Null while the state is unknown (Home Assistant didn't answer).</summary>
	protected bool? On { get; private set; }

	protected bool Busy { get; private set; }
	protected string? Error { get; private set; }

	private IDisposable? _events;
	private bool _disposed;

	protected override async Task OnInitializedAsync()
	{
		Light.Changed += OnLightChanged;
		_events = Context.Subscribe(e =>
		{
			if (e.Kind == PrinterEventKind.SettingsChanged) _ = ReloadAsync();
		});
		await LoadAsync();
	}

	private void OnLightChanged(LightSource source) => _ = ReloadAsync();

	private async Task LoadAsync()
	{
		Visible = await Context.GetUserAsync() is { IsAdmin: true }
			&& (await Context.GetEditableSettingsAsync<HomeAssistantSettings>()).Settings.HasLight;
		if (!Visible) return;
		PluginResult<bool> state = await Light.GetStateAsync();
		On = state.IsSuccess ? state.Value : null;
		Error = state.Error;
	}

	private async Task ReloadAsync()
	{
		if (_disposed) return;
		try
		{
			await InvokeAsync(async () =>
			{
				await LoadAsync();
				StateHasChanged();
			});
		}
		catch (ObjectDisposedException) { }
	}

	protected async Task ToggleAsync()
	{
		Busy = true;
		try
		{
			PluginResult result = await Light.SetAsync(On != true, Context, LightSource.Manual);
			if (!result.IsSuccess) Error = result.Error;
		}
		finally { Busy = false; }
	}

	public void Dispose()
	{
		_disposed = true;
		Light.Changed -= OnLightChanged;
		_events?.Dispose();
		GC.SuppressFinalize(this);
	}
}
