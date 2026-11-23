using PrintDeck.Plugins;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Modules.HomeAssistant.Tests.TestDoubles;

/// <summary>
/// What PrintDeck gives a module, in memory: settings, a user, terminal lines and the event stream. Like the real
/// context, only the module itself reads secrets and only admins save.
/// </summary>
internal sealed class FakePluginContext(PluginUser? user = null) : IPluginContext
{
	public static readonly PluginUser Module = new("module:home-assistant", IsAdmin: true, IsModule: true);
	public static readonly PluginUser Admin = new("ada", IsAdmin: true, IsModule: false);
	public static readonly PluginUser Viewer = new("vic", IsAdmin: false, IsModule: false);

	private readonly Lock _gate = new();
	private readonly List<Action<PrinterEvent>> _handlers = [];

	public PluginUser? User { get; set; } = user ?? Module;
	public HomeAssistantSettings Settings { get; set; } = new();
	public bool Enabled { get; set; } = true;
	public List<string> Logs { get; } = [];
	public int Subscribers
	{
		get { lock (_gate) return _handlers.Count; }
	}

	public string ModuleId => HomeAssistantModule.ModuleId;

	public Task<PluginUser?> GetUserAsync() => Task.FromResult(User);

	public Task<PrinterSnapshot> GetPrinterAsync(CancellationToken ct = default) =>
		Task.FromResult(new PrinterSnapshot(PrinterActivity.Idle, null, []));

	public IDisposable Subscribe(Action<PrinterEvent> handler)
	{
		lock (_gate) _handlers.Add(handler);
		return new Unsubscribe(() => { lock (_gate) _handlers.Remove(handler); });
	}

	private sealed class Unsubscribe(Action action) : IDisposable
	{
		public void Dispose() => action();
	}

	public void Raise(PrinterEventKind kind, string? file = "cube.gcode")
	{
		Action<PrinterEvent>[] handlers;
		lock (_gate) handlers = [.. _handlers];
		foreach (Action<PrinterEvent> h in handlers) h(new PrinterEvent(kind, DateTimeOffset.UnixEpoch, file));
	}

	public Task<PluginResult> SendGcodeAsync(string gcode, CancellationToken ct = default) => Task.FromResult(PluginResult.Success());
	public Task<PluginResult> StartPrintAsync(string fileName, CancellationToken ct = default) => Task.FromResult(PluginResult.Success());
	public Task<PluginResult> PauseAsync(CancellationToken ct = default) => Task.FromResult(PluginResult.Success());
	public Task<PluginResult> ResumeAsync(CancellationToken ct = default) => Task.FromResult(PluginResult.Success());
	public Task<PluginResult> CancelAsync(CancellationToken ct = default) => Task.FromResult(PluginResult.Success());

	public Task<PluginResult> LogAsync(string message, CancellationToken ct = default)
	{
		lock (_gate) Logs.Add(message);
		return Task.FromResult(PluginResult.Success());
	}

	public Task<bool> IsEnabledAsync(CancellationToken ct = default) => Task.FromResult(Enabled);

	public Task<PluginResult<T>> GetSettingsAsync<T>(CancellationToken ct = default) where T : class, new() =>
		Task.FromResult(User is { IsModule: true }
			? PluginResult.Success((T) (object) Copy(Settings))
			: PluginResult.Failure<T>("Only the module itself can do that."));

	public Task<EditableSettings<T>> GetEditableSettingsAsync<T>(CancellationToken ct = default) where T : class, new()
	{
		HomeAssistantSettings copy = Copy(Settings);
		HashSet<string> saved = copy.Token.Length > 0 ? [nameof(HomeAssistantSettings.Token)] : [];
		copy.Token = "";
		return Task.FromResult(new EditableSettings<T>((T) (object) copy, saved));
	}

	public Task<PluginResult> SaveSettingsAsync<T>(T settings, CancellationToken ct = default) where T : class, new()
	{
		if (User is not { IsAdmin: true }) return Task.FromResult(PluginResult.Failure("Only admins can do that."));
		HomeAssistantSettings s = Copy((HomeAssistantSettings) (object) settings);
		if (s.Token.Length == 0) s.Token = Settings.Token;
		Settings = s;
		return Task.FromResult(PluginResult.Success());
	}

	private static HomeAssistantSettings Copy(HomeAssistantSettings s) => new()
	{
		Url = s.Url,
		Token = s.Token,
		LightEntity = s.LightEntity,
		LightWhilePrinting = s.LightWhilePrinting,
		LightForTimelapse = s.LightForTimelapse,
		LightOffDelayMinutes = s.LightOffDelayMinutes
	};
}
