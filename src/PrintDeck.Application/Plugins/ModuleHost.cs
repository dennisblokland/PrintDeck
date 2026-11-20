using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Settings;
using PrintDeck.Plugins;

namespace PrintDeck.Application.Plugins;

/// <summary>
/// Starts each module that is switched on, when PrintDeck starts and whenever it is switched on in the settings, and
/// stops it when it is switched off. A module that fails to start is logged and left alone until it is switched off
/// and on again; it never stops PrintDeck.
/// </summary>
public sealed class ModuleHost(
	ModuleCatalog catalog,
	ISettingsStore settings,
	PrinterEventHub events,
	IModuleContexts contexts,
	ILogger<ModuleHost> logger) : IAsyncDisposable
{
	private readonly Lock _gate = new();
	private readonly Dictionary<string, (IPrintDeckModule Module, ModuleContextLease Lease)> _running = [];
	private readonly HashSet<string> _failed = [];
	private Task _work = Task.CompletedTask;
	private bool _stopped;

	public void Start()
	{
		events.Changed += OnChanged;
		Queue(SyncAsync);
	}

	/// <summary>Completes once queued starts and stops are done (for tests).</summary>
	public Task Pending
	{
		get { lock (_gate) return _work; }
	}

	public bool IsRunning(string moduleId)
	{
		lock (_gate) return _running.ContainsKey(moduleId);
	}

	private void OnChanged(PrinterChange change)
	{
		if ((change & PrinterChange.Settings) != 0) Queue(SyncAsync);
	}

	private void Queue(Func<Task> work)
	{
		lock (_gate) _work = _work.ContinueWith(_ => work(), TaskScheduler.Default).Unwrap();
	}

	private async Task SyncAsync()
	{
		HostSettings s;
		try { s = await settings.GetAsync(CancellationToken.None); }
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not read which modules are on");
			return;
		}

		bool changed = false;
		foreach (IPrintDeckModule module in catalog.All)
		{
			bool on = s.Modules.TryGetValue(module.Id, out ModuleSection? section) && section.Enabled;
			bool running;
			lock (_gate)
			{
				if (_stopped) return;
				running = _running.ContainsKey(module.Id);
				if (!on) _failed.Remove(module.Id);
				else if (_failed.Contains(module.Id)) continue;
			}
			if (on && !running) changed |= await StartModuleAsync(module);
			else if (!on && running) changed |= await StopModuleAsync(module.Id);
		}
		// Pages show a module's components only while it runs.
		if (changed) events.Publish(PrinterChange.Settings);
	}

	private async Task<bool> StartModuleAsync(IPrintDeckModule module)
	{
		ModuleContextLease lease = contexts.Create(module.Id);
		try
		{
			await module.StartAsync(lease.Context, lease.Services, CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Module {Module} did not start", module.Name);
			lock (_gate) _failed.Add(module.Id);
			await lease.DisposeAsync();
			return false;
		}
		lock (_gate) _running[module.Id] = (module, lease);
		logger.LogInformation("Started module {Module} {Version}", module.Name, module.Version);
		return true;
	}

	private async Task<bool> StopModuleAsync(string moduleId)
	{
		(IPrintDeckModule Module, ModuleContextLease Lease) running;
		lock (_gate)
		{
			if (!_running.Remove(moduleId, out running)) return false;
		}
		try { await running.Module.StopAsync(CancellationToken.None); }
		catch (Exception ex) { logger.LogWarning(ex, "Module {Module} did not stop cleanly", running.Module.Name); }
		await running.Lease.DisposeAsync();
		logger.LogInformation("Stopped module {Module}", running.Module.Name);
		return true;
	}

	public async ValueTask DisposeAsync()
	{
		events.Changed -= OnChanged;
		Task pending;
		lock (_gate)
		{
			_stopped = true;
			pending = _work;
		}
		try { await pending; }
		catch { /* each step logs its own failures */ }

		string[] ids;
		lock (_gate) ids = [.. _running.Keys];
		foreach (string id in ids) await StopModuleAsync(id);
	}
}
