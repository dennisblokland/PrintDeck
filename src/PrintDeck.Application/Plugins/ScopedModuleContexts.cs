using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Application.Plugins;

/// <summary>Gives each running module a scope of its own that acts as the module (<see cref="CurrentUserInfo.ForModule"/>).</summary>
public sealed class ScopedModuleContexts(IServiceScopeFactory scopes) : IModuleContexts
{
	public ModuleContextLease Create(string moduleId)
	{
		AsyncServiceScope scope = scopes.CreateAsyncScope();
		scope.ServiceProvider.GetRequiredService<ScopeIdentity>().User = CurrentUserInfo.ForModule(moduleId);
		PluginContext context = scope.ServiceProvider.GetRequiredService<PluginContextFactory>().Create(moduleId);
		return new ModuleContextLease(context, scope.ServiceProvider, async () =>
		{
			context.Dispose();
			await scope.DisposeAsync();
		});
	}
}
