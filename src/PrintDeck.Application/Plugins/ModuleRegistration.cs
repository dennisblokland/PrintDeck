using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Plugins;

namespace PrintDeck.Application.Plugins;

public static partial class ModuleRegistration
{
	[GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
	private static partial Regex ValidId();

	/// <summary>Adds an in-tree module: registers it and lets it register its own services.</summary>
	public static IServiceCollection AddPrintDeckModule(this IServiceCollection services, IPrintDeckModule module)
	{
		if (!ValidId().IsMatch(module.Id))
			throw new ArgumentException($"Module id '{module.Id}' must be lowercase letters, digits and dashes.", nameof(module));
		if (services.Any(d => d.ServiceType == typeof(IPrintDeckModule) && d.ImplementationInstance is IPrintDeckModule m && m.Id == module.Id))
			throw new InvalidOperationException($"Module '{module.Id}' is added twice.");
		if (module.SettingsType is { } type && type.GetConstructor(Type.EmptyTypes) is null)
			throw new ArgumentException($"The settings of module '{module.Id}' need a parameterless constructor.", nameof(module));

		services.AddSingleton(module);
		module.ConfigureServices(services);
		return services;
	}
}
