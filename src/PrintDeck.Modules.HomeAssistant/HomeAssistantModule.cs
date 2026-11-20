using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Modules.HomeAssistant.Components;
using PrintDeck.Modules.HomeAssistant.Light;
using PrintDeck.Plugins;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Modules.HomeAssistant;

/// <summary>
/// The printer light through Home Assistant: a toggle on the dashboard's camera card and the Control page, and
/// optionally on while printing or recording a timelapse. <c>VIRTUAL</c> as the URL gives an in-process light.
/// </summary>
public sealed class HomeAssistantModule : IPrintDeckModule
{
	public const string ModuleId = "home-assistant";

	public string Id => ModuleId;
	public string Name => "Home Assistant";
	public Version Version { get; } = new(1, 0);
	public Type SettingsType => typeof(HomeAssistantSettings);

	public IReadOnlyList<ModuleComponent> Components { get; } =
	[
		new(ModuleSlot.Settings, typeof(HomeAssistantSettingsCard)),
		new(ModuleSlot.CameraCard, typeof(LightButton)),
		new(ModuleSlot.Control, typeof(LightCard))
	];

	private LightAutomation? _automation;
	private PrinterLight? _light;

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddSingleton<VirtualHomeAssistant>();
		services.AddSingleton<HomeAssistantConnector>();
		services.AddSingleton<PrinterLight>();
		services.AddSingleton<LightAutomation>();
	}

	public Task StartAsync(IPluginContext context, IServiceProvider services, CancellationToken ct)
	{
		_light = services.GetRequiredService<PrinterLight>();
		_automation = services.GetRequiredService<LightAutomation>();
		_light.Attach(context);
		_automation.Start(context);
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken ct)
	{
		_automation?.Stop();
		_light?.Detach();
		return Task.CompletedTask;
	}
}
