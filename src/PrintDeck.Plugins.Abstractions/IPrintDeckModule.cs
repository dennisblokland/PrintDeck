using Microsoft.Extensions.DependencyInjection;

namespace PrintDeck.Plugins;

/// <summary>
/// An optional feature, such as a smart-home integration, that plugs into PrintDeck without growing the core.
/// A module sees PrintDeck only through <see cref="IPluginContext"/>. It is switched on and off in
/// Settings → Modules; a module that is off is never started and shows nothing.
/// </summary>
public interface IPrintDeckModule
{
	/// <summary>Stable id: lowercase letters, digits and dashes. Names the module's section in the settings.</summary>
	string Id { get; }

	/// <summary>The name shown in Settings → Modules.</summary>
	string Name { get; }

	Version Version { get; }

	/// <summary>
	/// The module's settings: a class with a parameterless constructor, saved as JSON. Properties marked
	/// <see cref="SecretAttribute"/> are never shown again after saving. Null when the module has no settings.
	/// </summary>
	Type? SettingsType { get; }

	/// <summary>Components PrintDeck renders in its pages, each given the module's context as <c>Context</c>.</summary>
	IReadOnlyList<ModuleComponent> Components { get; }

	/// <summary>Registers the module's own services, once, before PrintDeck starts. Runs whether or not the module is on.</summary>
	void ConfigureServices(IServiceCollection services);

	/// <summary>
	/// Called when PrintDeck starts with the module on, and when it is switched on. <paramref name="context"/> acts as the
	/// module itself (not as a signed-in user) and stays valid until <see cref="StopAsync"/>. <paramref name="services"/>
	/// resolves the services the module registered in <see cref="ConfigureServices"/>.
	/// </summary>
	Task StartAsync(IPluginContext context, IServiceProvider services, CancellationToken ct);

	/// <summary>Called when the module is switched off and when PrintDeck stops. Its event subscriptions end with it.</summary>
	Task StopAsync(CancellationToken ct);
}
