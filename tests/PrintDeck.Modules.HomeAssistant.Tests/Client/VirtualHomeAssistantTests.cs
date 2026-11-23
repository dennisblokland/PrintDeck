using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Tests.Client;

public sealed class VirtualHomeAssistantTests
{
	private readonly VirtualHomeAssistant _ha = new();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Has_a_printer_light_that_starts_off()
	{
		PluginResult<EntityState> light = await _ha.GetStateAsync(VirtualHomeAssistant.Light, Ct);

		Assert.False(light.Value!.IsOn);
		Assert.Contains((await _ha.ListStatesAsync(Ct)).Value!, s => s.EntityId == VirtualHomeAssistant.Power);
	}

	[Fact]
	public async Task Switches_on_off_and_toggles()
	{
		await _ha.CallServiceAsync("light", "turn_on", VirtualHomeAssistant.Light, Ct);
		Assert.True((await _ha.GetStateAsync(VirtualHomeAssistant.Light, Ct)).Value!.IsOn);

		await _ha.CallServiceAsync("light", "toggle", VirtualHomeAssistant.Light, Ct);
		Assert.False((await _ha.GetStateAsync(VirtualHomeAssistant.Light, Ct)).Value!.IsOn);

		Assert.Equal(["light.turn_on light.printer", "light.toggle light.printer"], _ha.Calls);
	}

	[Fact]
	public async Task Refuses_unknown_entities_and_the_wrong_domain()
	{
		Assert.Equal("Home Assistant has no light.nope.", (await _ha.GetStateAsync("light.nope", Ct)).Error);
		Assert.Equal("Home Assistant has no light.printer.", (await _ha.CallServiceAsync("switch", "turn_on", VirtualHomeAssistant.Light, Ct)).Error);
		Assert.Empty(_ha.Calls);
	}
}
