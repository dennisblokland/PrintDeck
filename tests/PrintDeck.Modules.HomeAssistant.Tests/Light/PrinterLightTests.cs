using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Modules.HomeAssistant.Light;
using PrintDeck.Modules.HomeAssistant.Light.Enums;
using PrintDeck.Modules.HomeAssistant.Tests.TestDoubles;
using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Tests.Light;

public sealed class PrinterLightTests : IDisposable
{
	private readonly VirtualHomeAssistant _ha = new();
	private readonly HomeAssistantConnector _connector;
	private readonly PrinterLight _light;
	private readonly FakePluginContext _module = new();

	public PrinterLightTests()
	{
		_connector = new HomeAssistantConnector(_ha);
		_light = new PrinterLight(_connector);
		_module.Settings = new HomeAssistantSettings { Url = "VIRTUAL", LightEntity = VirtualHomeAssistant.Light };
		_light.Attach(_module);
	}

	public void Dispose() => _connector.Dispose();

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task An_admin_switches_the_light_and_the_terminal_says_so()
	{
		List<LightSource> changes = [];
		_light.Changed += changes.Add;

		PluginResult on = await _light.SetAsync(true, new FakePluginContext(FakePluginContext.Admin), LightSource.Manual, Ct);

		Assert.True(on.IsSuccess);
		Assert.True((await _light.GetStateAsync(Ct)).Value);
		Assert.Equal(["light.turn_on light.printer"], _ha.Calls);
		Assert.Equal(["Light on through Home Assistant"], _module.Logs);
		Assert.Equal([LightSource.Manual], changes);
	}

	[Fact]
	public async Task Switches_a_switch_entity_through_its_own_domain()
	{
		_module.Settings.LightEntity = VirtualHomeAssistant.Power;

		await _light.SetAsync(false, _module, LightSource.Automation, Ct);

		Assert.Equal(["switch.turn_off switch.printer_power"], _ha.Calls);
		Assert.Equal(["Light off through Home Assistant"], _module.Logs);
	}

	[Fact]
	public async Task A_viewer_cannot_switch_it()
	{
		PluginResult on = await _light.SetAsync(true, new FakePluginContext(FakePluginContext.Viewer), LightSource.Manual, Ct);

		Assert.Equal(PrinterLight.AdminsOnly, on.Error);
		Assert.Empty(_ha.Calls);
	}

	[Fact]
	public async Task Needs_a_light_and_a_running_module()
	{
		_module.Settings.LightEntity = "";
		Assert.Equal(HomeAssistantErrors.NoLight, (await _light.GetStateAsync(Ct)).Error);

		_light.Detach();
		Assert.Equal(HomeAssistantErrors.Off, (await _light.SetAsync(true, _module, LightSource.Manual, Ct)).Error);
	}

	[Fact]
	public async Task A_real_home_assistant_needs_a_url_and_token()
	{
		_module.Settings.Url = "";
		Assert.Equal(HomeAssistantErrors.NoUrl, (await _light.GetStateAsync(Ct)).Error);

		_module.Settings.Url = "http://ha.local:8123";
		Assert.Equal(HomeAssistantErrors.NoToken, (await _light.GetStateAsync(Ct)).Error);
	}

	[Fact]
	public async Task Test_lists_the_lights_and_switches()
	{
		PluginResult<IReadOnlyList<EntityState>> found = await _light.TestAsync(
			new HomeAssistantSettings { Url = " VIRTUAL " }, new FakePluginContext(FakePluginContext.Admin), Ct);

		Assert.Equal([VirtualHomeAssistant.Light, VirtualHomeAssistant.Power], found.Value!.Select(e => e.EntityId));
	}

	[Fact]
	public async Task Test_uses_the_saved_token_when_none_is_typed()
	{
		_module.Settings.Token = "SAVED";
		PluginResult<IReadOnlyList<EntityState>> found = await _light.TestAsync(
			new HomeAssistantSettings { Url = "http://127.0.0.1:9" }, new FakePluginContext(FakePluginContext.Admin), Ct);

		// Got past "add a token": the saved one was used, and nothing listens on port 9.
		Assert.Equal(HomeAssistantErrors.NoAnswer, found.Error);
	}

	[Fact]
	public async Task Only_admins_test()
	{
		PluginResult<IReadOnlyList<EntityState>> found = await _light.TestAsync(
			new HomeAssistantSettings { Url = "VIRTUAL" }, new FakePluginContext(FakePluginContext.Viewer), Ct);

		Assert.Equal(PrinterLight.AdminsOnly, found.Error);
	}
}
