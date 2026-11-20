using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Modules.HomeAssistant.Light;
using PrintDeck.Modules.HomeAssistant.Light.Enums;
using PrintDeck.Modules.HomeAssistant.Tests.TestDoubles;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Modules.HomeAssistant.Tests.Light;

public sealed class LightAutomationTests : IDisposable
{
	private readonly VirtualHomeAssistant _ha = new();
	private readonly HomeAssistantConnector _connector;
	private readonly PrinterLight _light;
	private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));
	private readonly LightAutomation _automation;
	private readonly FakePluginContext _module = new();

	public LightAutomationTests()
	{
		_connector = new HomeAssistantConnector(_ha);
		_light = new PrinterLight(_connector);
		_module.Settings = new HomeAssistantSettings
		{
			Url = "VIRTUAL",
			LightEntity = VirtualHomeAssistant.Light,
			LightWhilePrinting = true,
			LightOffDelayMinutes = 3
		};
		_light.Attach(_module);
		_automation = new LightAutomation(_light, _time);
		_automation.Start(_module);
	}

	public void Dispose()
	{
		_automation.Dispose();
		_connector.Dispose();
	}

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task Raise(PrinterEventKind kind)
	{
		_module.Raise(kind);
		await _automation.Pending;
	}

	private async Task<bool> LightIsOn() => (await _ha.GetStateAsync(VirtualHomeAssistant.Light, Ct)).Value!.IsOn;

	[Fact]
	public async Task On_while_printing_and_off_a_while_after()
	{
		await Raise(PrinterEventKind.PrintStarted);
		Assert.True(await LightIsOn());

		await Raise(PrinterEventKind.PrintDone);
		Assert.True(await LightIsOn());
		Assert.True(_automation.OffScheduled);

		_time.Advance(TimeSpan.FromMinutes(2));
		await _automation.Pending;
		Assert.True(await LightIsOn());

		_time.Advance(TimeSpan.FromMinutes(1));
		await Eventually.Until(() => _ha.Calls.Count == 2);
		await _automation.Pending;
		Assert.False(await LightIsOn());
		Assert.Equal(["Light on through Home Assistant", "Light off through Home Assistant"], _module.Logs);
	}

	[Theory]
	[InlineData(PrinterEventKind.PrintFailed)]
	[InlineData(PrinterEventKind.PrintCancelled)]
	public async Task Off_after_a_print_that_did_not_finish_too(PrinterEventKind end)
	{
		_module.Settings.LightOffDelayMinutes = 0;
		await Raise(PrinterEventKind.PrintStarted);

		await Raise(end);

		await Eventually.Until(() => _ha.Calls.Count == 2);
		Assert.False(await LightIsOn());
	}

	[Fact]
	public async Task A_new_print_cancels_the_switch_off()
	{
		await Raise(PrinterEventKind.PrintStarted);
		await Raise(PrinterEventKind.PrintDone);

		await Raise(PrinterEventKind.PrintStarted);
		_time.Advance(TimeSpan.FromMinutes(10));
		await _automation.Pending;

		Assert.False(_automation.OffScheduled);
		Assert.True(await LightIsOn());
		Assert.Equal(["light.turn_on light.printer"], _ha.Calls);
	}

	[Fact]
	public async Task Leaves_a_light_that_was_already_on_alone()
	{
		await _ha.CallServiceAsync("light", "turn_on", VirtualHomeAssistant.Light, Ct);

		await Raise(PrinterEventKind.PrintStarted);
		await Raise(PrinterEventKind.PrintDone);
		_time.Advance(TimeSpan.FromMinutes(10));
		await _automation.Pending;

		Assert.True(await LightIsOn());
		Assert.Equal(["light.turn_on light.printer"], _ha.Calls);
	}

	[Fact]
	public async Task Someone_switching_the_light_takes_it_over()
	{
		await Raise(PrinterEventKind.PrintStarted);
		await Raise(PrinterEventKind.PrintDone);

		await _light.SetAsync(true, new FakePluginContext(FakePluginContext.Admin), LightSource.Manual, Ct);
		_time.Advance(TimeSpan.FromMinutes(10));
		await _automation.Pending;

		Assert.False(_automation.OffScheduled);
		Assert.True(await LightIsOn());
	}

	[Fact]
	public async Task Does_nothing_unless_chosen()
	{
		_module.Settings.LightWhilePrinting = false;

		await Raise(PrinterEventKind.PrintStarted);
		await Raise(PrinterEventKind.TimelapseStarted);

		Assert.Empty(_ha.Calls);
	}

	[Fact]
	public async Task Stays_on_for_a_timelapse_that_is_still_rendering()
	{
		_module.Settings.LightWhilePrinting = false;
		_module.Settings.LightForTimelapse = true;

		await Raise(PrinterEventKind.PrintStarted);
		Assert.False(await LightIsOn());
		await Raise(PrinterEventKind.TimelapseStarted);
		Assert.True(await LightIsOn());

		await Raise(PrinterEventKind.PrintDone);
		Assert.False(_automation.OffScheduled);

		await Raise(PrinterEventKind.TimelapseStopped);
		Assert.True(_automation.OffScheduled);
		_time.Advance(TimeSpan.FromMinutes(3));
		await Eventually.Until(() => _ha.Calls.Count == 2);
		Assert.False(await LightIsOn());
	}

	[Fact]
	public async Task Stays_on_while_still_printing_after_the_timelapse()
	{
		_module.Settings.LightForTimelapse = true;

		await Raise(PrinterEventKind.PrintStarted);
		await Raise(PrinterEventKind.TimelapseStarted);
		await Raise(PrinterEventKind.TimelapseStopped);

		Assert.False(_automation.OffScheduled);
		Assert.True(await LightIsOn());
	}

	[Fact]
	public async Task Logs_when_home_assistant_fails()
	{
		_module.Settings.LightEntity = "light.gone";

		await Raise(PrinterEventKind.PrintStarted);

		Assert.Equal(["Could not switch the light on: Home Assistant has no light.gone."], _module.Logs);
	}

	[Fact]
	public async Task Stop_ends_the_subscription_and_the_timer()
	{
		await Raise(PrinterEventKind.PrintStarted);
		await Raise(PrinterEventKind.PrintDone);

		_automation.Stop();

		Assert.Equal(0, _module.Subscribers);
		Assert.Equal(0, _time.ActiveTimers);
	}
}
