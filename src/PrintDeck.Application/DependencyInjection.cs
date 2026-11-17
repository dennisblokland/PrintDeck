using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;

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
		return services;
	}

	/// <summary>Starts recording finished prints into each file's history.</summary>
	public static void StartPrintHistory(this IServiceProvider services) =>
		services.GetRequiredService<PrintHistoryRecorder>().Start();

	/// <summary>Starts watching for the last port, when auto-connect is on.</summary>
	public static void StartAutoConnect(this IServiceProvider services) =>
		services.GetRequiredService<AutoConnector>().Start();
}
