using PrintDeck.Modules.HomeAssistant.Light.Enums;
using PrintDeck.Plugins;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Modules.HomeAssistant.Light;

/// <summary>
/// Switches the light on when a print starts or a timelapse starts recording (each when chosen in the settings), and
/// off <see cref="HomeAssistantSettings.LightOffDelayMinutes"/> after the last of them ends. It only switches off a
/// light it switched on itself: a light that was already on, or that someone switched in between, is left alone.
/// </summary>
public sealed class LightAutomation(PrinterLight light, TimeProvider time) : IDisposable
{
	private readonly Lock _gate = new();
	private IPluginContext? _module;
	private IDisposable? _subscription;
	private Task _work = Task.CompletedTask;
	private ITimer? _off;
	private bool _owns;
	private bool _printing;
	private bool _timelapse;

	/// <summary>Completes once the queued switches are done (for tests).</summary>
	public Task Pending
	{
		get { lock (_gate) return _work; }
	}

	/// <summary>A switch-off is waiting for its delay.</summary>
	public bool OffScheduled
	{
		get { lock (_gate) return _off is not null; }
	}

	public void Start(IPluginContext module)
	{
		lock (_gate)
		{
			_module = module;
			_owns = _printing = _timelapse = false;
		}
		light.Changed += OnLightChanged;
		_subscription = module.Subscribe(OnEvent);
	}

	public void Stop()
	{
		_subscription?.Dispose();
		_subscription = null;
		light.Changed -= OnLightChanged;
		lock (_gate)
		{
			_module = null;
			CancelOff();
		}
	}

	private void OnEvent(PrinterEvent e)
	{
		lock (_gate)
		{
			switch (e.Kind)
			{
				case PrinterEventKind.PrintStarted:
					_printing = true;
					CancelOff();
					Queue(() => SwitchOnAsync(s => s.LightWhilePrinting));
					break;
				case PrinterEventKind.TimelapseStarted:
					_timelapse = true;
					CancelOff();
					Queue(() => SwitchOnAsync(s => s.LightForTimelapse));
					break;
				case PrinterEventKind.PrintDone or PrinterEventKind.PrintFailed or PrinterEventKind.PrintCancelled:
					_printing = false;
					Queue(ScheduleOffAsync);
					break;
				case PrinterEventKind.TimelapseStopped:
					_timelapse = false;
					Queue(ScheduleOffAsync);
					break;
			}
		}
	}

	private void OnLightChanged(LightSource source)
	{
		if (source != LightSource.Manual) return;
		lock (_gate)
		{
			_owns = false;
			CancelOff();
		}
	}

	/// <summary>Call under the lock.</summary>
	private void Queue(Func<Task> work) => _work = _work.ContinueWith(_ => work(), TaskScheduler.Default).Unwrap();

	/// <summary>Call under the lock.</summary>
	private void CancelOff()
	{
		_off?.Dispose();
		_off = null;
	}

	private async Task<HomeAssistantSettings?> SettingsAsync(IPluginContext module)
	{
		PluginResult<HomeAssistantSettings> s = await module.GetSettingsAsync<HomeAssistantSettings>();
		return s.IsSuccess && s.Value!.HasLight ? s.Value : null;
	}

	/// <summary>Whether something the settings keep the light on for is still going.</summary>
	private bool Needed(HomeAssistantSettings s)
	{
		lock (_gate) return _printing && s.LightWhilePrinting || _timelapse && s.LightForTimelapse;
	}

	private async Task SwitchOnAsync(Func<HomeAssistantSettings, bool> wanted)
	{
		IPluginContext? module;
		lock (_gate) module = _module;
		if (module is null || await SettingsAsync(module) is not { } s || !wanted(s)) return;
		lock (_gate) if (_owns) return;

		PluginResult<bool> state = await light.GetStateAsync();
		if (!state.IsSuccess)
		{
			await module.LogAsync($"Could not switch the light on: {state.Error}");
			return;
		}
		if (state.Value) return; // already on: someone else's light, left alone

		PluginResult on = await light.SetAsync(true, module, LightSource.Automation);
		if (on.IsSuccess)
		{
			lock (_gate) _owns = true;
		}
		else await module.LogAsync($"Could not switch the light on: {on.Error}");
	}

	private async Task ScheduleOffAsync()
	{
		IPluginContext? module;
		lock (_gate)
		{
			module = _module;
			if (module is null || !_owns) return;
		}
		if (await SettingsAsync(module) is not { } s || Needed(s)) return;

		lock (_gate)
		{
			if (!_owns || _module is null) return;
			CancelOff();
			ITimer? timer = null;
			timer = time.CreateTimer(_ =>
			{
				lock (_gate)
				{
					if (!ReferenceEquals(_off, timer)) return; // cancelled while it fired
					CancelOff();
					Queue(SwitchOffAsync);
				}
			}, null, TimeSpan.FromMinutes(s.LightOffDelayMinutes), Timeout.InfiniteTimeSpan);
			_off = timer;
		}
	}

	private async Task SwitchOffAsync()
	{
		IPluginContext? module;
		lock (_gate)
		{
			module = _module;
			if (module is null || !_owns) return;
		}
		if (await SettingsAsync(module) is { } s && Needed(s)) return;

		PluginResult off = await light.SetAsync(false, module, LightSource.Automation);
		if (off.IsSuccess)
		{
			lock (_gate) _owns = false;
		}
		else await module.LogAsync($"Could not switch the light off: {off.Error}");
	}

	public void Dispose() => Stop();
}
