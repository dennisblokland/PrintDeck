using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Camera;
using PrintDeck.Application.Files;
using PrintDeck.Application.Files.Thumbnails;
using PrintDeck.Application.Plugins;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Application.Timelapse;

namespace PrintDeck.Application;

public static class DependencyInjection
{
	/// <summary>Core services. The host registers Mediator itself (the source generator runs there).</summary>
	public static IServiceCollection AddApplicationCore(this IServiceCollection services)
	{
		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<PrinterEventHub>();
		services.AddSingleton<IPrinterEvents>(sp => sp.GetRequiredService<PrinterEventHub>());
		services.AddSingleton<ToolpathCache>();
		services.AddSingleton<PrinterSession>();
		services.AddSingleton<IPrinterSession>(sp => sp.GetRequiredService<PrinterSession>());
		services.AddSingleton<PrintHistoryRecorder>();
		services.AddSingleton<AutoConnector>();
		services.AddSingleton<ThumbnailBackfill>();
		services.AddSingleton<CameraControlsStartup>();
		services.AddSingleton<TimelapseRecorder>();
		services.AddSingleton<AppKeyRequests>();
		services.AddSingleton<PrinterEventStream>();
		services.AddSingleton<ModuleCatalog>();
		services.AddSingleton<ModuleHost>();
		services.AddSingleton<IModuleContexts, ScopedModuleContexts>();
		services.AddScoped<PluginContextFactory>();
		services.AddScoped<ScopeIdentity>();
		return services;
	}

	/// <summary>Starts the printer event stream and every module that is switched on. Call after the other Start* calls.</summary>
	public static void StartModules(this IServiceProvider services)
	{
		services.GetRequiredService<PrinterEventStream>().Start();
		services.GetRequiredService<ModuleHost>().Start();
	}

	/// <summary>Starts recording finished prints into each file's history.</summary>
	public static void StartPrintHistory(this IServiceProvider services) =>
		services.GetRequiredService<PrintHistoryRecorder>().Start();

	/// <summary>Starts watching for the last port, when auto-connect is on.</summary>
	public static void StartAutoConnect(this IServiceProvider services) =>
		services.GetRequiredService<AutoConnector>().Start();

	/// <summary>Reads slicer thumbnails for stored files that have none yet, in the background.</summary>
	public static void StartThumbnailBackfill(this IServiceProvider services) =>
		_ = services.GetRequiredService<ThumbnailBackfill>().Start();

	/// <summary>Gives this PrintDeck its stable id on first start (OctoPrint clients tell instances apart by it).</summary>
	public static async Task EnsureInstanceIdAsync(this IServiceProvider services)
	{
		ISettingsStore settings = services.GetRequiredService<ISettingsStore>();
		if ((await settings.GetAsync(CancellationToken.None)).InstanceId.Length == 0)
			await settings.UpdateAsync(s => s.InstanceId.Length == 0 ? s with { InstanceId = Guid.NewGuid().ToString("D") } : s, CancellationToken.None);
	}

	/// <summary>Starts recording timelapses of prints, when they are switched on in the settings.</summary>
	public static void StartTimelapse(this IServiceProvider services) =>
		services.GetRequiredService<TimelapseRecorder>().Start();

	/// <summary>Applies the saved camera controls in the background, so a slow or missing camera doesn't hold up the start.</summary>
	public static void StartCameraControls(this IServiceProvider services) =>
		_ = Task.Run(() => services.GetRequiredService<CameraControlsStartup>().ApplySavedAsync(CancellationToken.None));
}
