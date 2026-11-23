using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Plugins;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Plugins;

public sealed class ModuleHostTests
{
	private readonly FakeModule _lights = new("lights");
	private readonly FakeModule _broken = new("broken", failToStart: true);
	private readonly FakeSettingsStore _settings = new();
	private readonly FakeModuleContexts _contexts = new();
	private readonly ModuleHost _host;
	private readonly List<PrinterChange> _published;
	private readonly PrinterEventHub _events;

	public ModuleHostTests()
	{
		(_events, _published) = RecordingEventHub.Create();
		_host = new ModuleHost(new ModuleCatalog([_lights, _broken]), _settings, _events, _contexts, NullLogger<ModuleHost>.Instance);
	}

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task Switch(string id, bool on)
	{
		await _settings.UpdateAsync(s => s with
		{
			Modules = new Dictionary<string, ModuleSection>(s.Modules) { [id] = new ModuleSection(on) }
		}, Ct);
		_events.Publish(PrinterChange.Settings);
		await _host.Pending;
	}

	[Fact]
	public async Task A_module_that_is_off_is_never_started()
	{
		_host.Start();
		await _host.Pending;

		Assert.Empty(_lights.Calls);
		Assert.Empty(_contexts.Created);
		Assert.False(_host.IsRunning("lights"));
	}

	[Fact]
	public async Task Starts_modules_that_are_on_when_printdeck_starts()
	{
		await _settings.UpdateAsync(s => s with { Modules = new Dictionary<string, ModuleSection> { ["lights"] = new(true) } }, Ct);

		_host.Start();
		await _host.Pending;

		Assert.Equal(["start"], _lights.Calls);
		Assert.Equal("lights", _lights.Context!.ModuleId);
		Assert.True(_host.IsRunning("lights"));
	}

	[Fact]
	public async Task Switching_a_module_starts_and_stops_it_without_a_restart()
	{
		_host.Start();
		await _host.Pending;

		await Switch("lights", true);
		Assert.True(_host.IsRunning("lights"));
		Assert.Contains(PrinterChange.Settings, _published);

		_published.Clear();
		await Switch("lights", false);

		Assert.Equal(["start", "stop"], _lights.Calls);
		Assert.Equal(["lights"], _contexts.Released);
		Assert.False(_host.IsRunning("lights"));
		Assert.Equal([PrinterChange.Settings, PrinterChange.Settings], _published); // the switch, then "it stopped"
	}

	[Fact]
	public async Task Other_settings_changes_leave_running_modules_alone()
	{
		_host.Start();
		await Switch("lights", true);

		_events.Publish(PrinterChange.Settings);
		await _host.Pending;

		Assert.Equal(["start"], _lights.Calls);
	}

	[Fact]
	public async Task A_module_that_fails_to_start_stays_off_and_the_rest_run()
	{
		await _settings.UpdateAsync(s => s with
		{
			Modules = new Dictionary<string, ModuleSection> { ["lights"] = new(true), ["broken"] = new(true) }
		}, Ct);

		_host.Start();
		await _host.Pending;

		Assert.False(_host.IsRunning("broken"));
		Assert.Equal(["broken"], _contexts.Released);
		Assert.True(_host.IsRunning("lights"));

		_events.Publish(PrinterChange.Settings);
		await _host.Pending;
		Assert.Equal(["start"], _broken.Calls); // not tried again on every settings change…

		await Switch("broken", false);
		await Switch("broken", true);
		Assert.Equal(["start", "start"], _broken.Calls); // …only when switched off and on
	}

	[Fact]
	public async Task Dispose_stops_every_running_module()
	{
		_host.Start();
		await Switch("lights", true);

		await _host.DisposeAsync();

		Assert.Equal(["start", "stop"], _lights.Calls);
		Assert.False(_host.IsRunning("lights"));
	}
}
