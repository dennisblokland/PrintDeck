using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using PrintDeck.Infrastructure.Camera;
using PrintDeck.Infrastructure.Health;
using PrintDeck.Infrastructure.HostControl;
using PrintDeck.Infrastructure.Identity;
using PrintDeck.Infrastructure.Logging;
using PrintDeck.Infrastructure.Serial;
using PrintDeck.Domain.Settings;
using PrintDeck.Infrastructure.Storage;
using PrintDeck.Infrastructure.Timelapse;
using PrintDeck.Infrastructure.Virtual;
using PrintDeck.Infrastructure.Processes;

namespace PrintDeck.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
		services.Configure<VirtualPrinterOptions>(configuration.GetSection(VirtualPrinterOptions.Section));
		services.AddSingleton<IPrinterTransportFactory, TransportFactory>();
		services.AddSingleton<IPortDiscovery, PortDiscovery>();
		services.AddSingleton<IGcodeFileStore, FileSystemGcodeStore>();
		services.AddSingleton<ISettingsStore, JsonSettingsStore>();
		services.AddSingleton<IBackupArchive, ZipBackupArchive>();
		services.Configure<HostCommandOptions>(configuration.GetSection(HostCommandOptions.Section));
		services.AddSingleton<IHostCommands, ShellHostCommands>();
		services.AddSingleton<ISerialLog, FileSerialLog>();
		services.AddSingleton<ILogFiles, LogDirectoryFiles>();
		services.AddSingleton<HttpCameraSnapshots>(_ => new HttpCameraSnapshots());
		services.AddSingleton<ICameraSnapshots>(sp => new CameraSnapshots(
			sp.GetRequiredService<HttpCameraSnapshots>(), new VirtualCamera(sp.GetRequiredService<TimeProvider>())));
		services.AddSingleton<ITimelapseStore>(sp => new FileSystemTimelapseStore(sp.GetRequiredService<IOptions<StorageOptions>>(), new ProcessCommandRunner()));
		services.AddSingleton<IHostHealth>(sp => new HostHealth(
			sp.GetRequiredService<IOptions<StorageOptions>>().Value.DataDirectory, new ProcessCommandRunner(), sp.GetRequiredService<TimeProvider>()));
		services.AddSingleton<ICameraControls>(_ => new V4l2CameraControls(new ProcessCommandRunner(), new VirtualV4l2Ctl(), V4l2CameraControls.HostDevices));
		services.AddSingleton<FileLoggerProvider>();
		services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(sp => sp.GetRequiredService<FileLoggerProvider>());
		services.AddDbContext<PrintDeckDbContext>((sp, o) =>
			o.UseSqlite($"Data Source={sp.GetRequiredService<IOptions<StorageOptions>>().Value.DatabaseFile}"));
		services.AddScoped<IUserAccounts, IdentityUserAccounts>();
		services.AddScoped<IApiKeys, IdentityApiKeys>();
		return services;
	}

	/// <summary>Turns the serial log on at startup when it was on before the last shutdown.</summary>
	public static async Task InitializeSerialLogAsync(this IServiceProvider services)
	{
		HostSettings settings = await services.GetRequiredService<ISettingsStore>().GetAsync(CancellationToken.None);
		services.GetRequiredService<ISerialLog>().SetEnabled(settings.SerialLogEnabled);
	}
}
