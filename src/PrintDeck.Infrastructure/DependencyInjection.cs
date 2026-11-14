using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Infrastructure.Serial;
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
		return services;
	}
}
