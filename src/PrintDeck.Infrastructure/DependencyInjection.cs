using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PrintDeck.Infrastructure.HostControl;
using PrintDeck.Infrastructure.Identity;
using PrintDeck.Infrastructure.Logging;
using PrintDeck.Infrastructure.Serial;
using PrintDeck.Domain.Settings;
using PrintDeck.Infrastructure.Storage;

namespace PrintDeck.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
		services.AddSingleton<IPrinterTransportFactory, TransportFactory>();
		services.AddSingleton<IPortDiscovery, PortDiscovery>();
		services.AddSingleton<IGcodeFileStore, FileSystemGcodeStore>();
		services.AddSingleton<ISettingsStore, JsonSettingsStore>();
		services.Configure<HostCommandOptions>(configuration.GetSection(HostCommandOptions.Section));
		services.AddSingleton<IHostCommands, ShellHostCommands>();
		services.AddSingleton<ISerialLog, FileSerialLog>();
		services.AddSingleton<ILogFiles, LogDirectoryFiles>();
		services.AddSingleton<FileLoggerProvider>();
		services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(sp => sp.GetRequiredService<FileLoggerProvider>());
		services.AddDbContext<PrintDeckDbContext>((sp, o) =>
			o.UseSqlite($"Data Source={sp.GetRequiredService<IOptions<StorageOptions>>().Value.DatabaseFile}"));
		services.AddScoped<IUserAccounts, IdentityUserAccounts>();
		return services;
	}

	/// <summary>Turns the serial log on at startup when it was on before the last shutdown.</summary>
	public static async Task InitializeSerialLogAsync(this IServiceProvider services)
	{
		HostSettings settings = await services.GetRequiredService<ISettingsStore>().GetAsync(CancellationToken.None);
		services.GetRequiredService<ISerialLog>().SetEnabled(settings.SerialLogEnabled);
	}
}
