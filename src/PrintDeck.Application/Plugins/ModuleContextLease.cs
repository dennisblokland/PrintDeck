using PrintDeck.Plugins;

namespace PrintDeck.Application.Plugins;

/// <summary>A module's own context and the services of its scope, valid until disposed; disposing ends its subscriptions.</summary>
public sealed class ModuleContextLease(IPluginContext context, IServiceProvider services, Func<ValueTask> release) : IAsyncDisposable
{
	public IPluginContext Context => context;
	public IServiceProvider Services => services;

	public ValueTask DisposeAsync() => release();
}
