using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Modules.HomeAssistant.Light;
using PrintDeck.Modules.HomeAssistant.Tests.TestDoubles;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Modules.HomeAssistant.Tests;

public sealed class HomeAssistantModuleTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public void Describes_itself()
	{
		HomeAssistantModule module = new();

		Assert.Equal("home-assistant", module.Id);
		Assert.Equal(typeof(HomeAssistantSettings), module.SettingsType);
		Assert.Equal([ModuleSlot.Settings, ModuleSlot.CameraCard, ModuleSlot.Control], module.Components.Select(c => c.Slot));
	}

	[Fact]
	public async Task Starts_with_its_own_services_and_stops_listening()
	{
		HomeAssistantModule module = new();
		ServiceCollection services = new();
		services.AddSingleton(TimeProvider.System);
		module.ConfigureServices(services);
		await using ServiceProvider provider = services.BuildServiceProvider();
		FakePluginContext context = new() { Settings = new HomeAssistantSettings { Url = "VIRTUAL", LightEntity = VirtualHomeAssistant.Light, LightWhilePrinting = true } };

		await module.StartAsync(context, provider, Ct);
		context.Raise(PrinterEventKind.PrintStarted);
		await provider.GetRequiredService<LightAutomation>().Pending;

		Assert.True((await provider.GetRequiredService<PrinterLight>().GetStateAsync(Ct)).Value);
		Assert.Equal(1, context.Subscribers);

		await module.StopAsync(Ct);
		Assert.Equal(0, context.Subscribers);
		Assert.Equal(HomeAssistantErrors.Off, (await provider.GetRequiredService<PrinterLight>().GetStateAsync(Ct)).Error);
	}
}
