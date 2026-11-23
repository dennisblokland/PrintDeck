namespace PrintDeck.Plugins;

/// <summary>
/// The only way a module reaches PrintDeck. Calls go through PrintDeck's own commands and queries, so the same
/// permission checks apply: the context a page gives a module's component acts as the signed-in user, the one
/// <see cref="IPrintDeckModule.StartAsync"/> gets acts as the module.
/// </summary>
public interface IPluginContext
{
	string ModuleId { get; }

	/// <summary>Who this context acts for; null when nobody is signed in.</summary>
	Task<PluginUser?> GetUserAsync();

	/// <summary>Connection state, the job and the latest temperatures.</summary>
	Task<PrinterSnapshot> GetPrinterAsync(CancellationToken ct = default);

	/// <summary>
	/// Calls <paramref name="handler"/> for each printer event until the returned subscription is disposed (or the module
	/// stops). Events arrive on PrintDeck's printer threads, one at a time: return quickly and do slow work elsewhere.
	/// </summary>
	IDisposable Subscribe(Action<PrinterEvent> handler);

	/// <summary>Sends G-code, one command per line, as typed in the terminal.</summary>
	Task<PluginResult> SendGcodeAsync(string gcode, CancellationToken ct = default);

	/// <summary>Starts printing a stored file.</summary>
	Task<PluginResult> StartPrintAsync(string fileName, CancellationToken ct = default);

	Task<PluginResult> PauseAsync(CancellationToken ct = default);
	Task<PluginResult> ResumeAsync(CancellationToken ct = default);
	Task<PluginResult> CancelAsync(CancellationToken ct = default);

	/// <summary>Adds an info line to the terminal, e.g. "Light on through Home Assistant".</summary>
	Task<PluginResult> LogAsync(string message, CancellationToken ct = default);

	/// <summary>Whether the module is switched on in Settings → Modules.</summary>
	Task<bool> IsEnabledAsync(CancellationToken ct = default);

	/// <summary>The module's settings, secrets included. Only the module's own context may read secrets; any other gets an error.</summary>
	Task<PluginResult<T>> GetSettingsAsync<T>(CancellationToken ct = default) where T : class, new();

	/// <summary>The module's settings for a form: secrets are empty, <see cref="EditableSettings{T}.SavedSecrets"/> says which are set.</summary>
	Task<EditableSettings<T>> GetEditableSettingsAsync<T>(CancellationToken ct = default) where T : class, new();

	/// <summary>Saves the module's settings. A secret left empty keeps its saved value. Needs an admin.</summary>
	Task<PluginResult> SaveSettingsAsync<T>(T settings, CancellationToken ct = default) where T : class, new();
}
