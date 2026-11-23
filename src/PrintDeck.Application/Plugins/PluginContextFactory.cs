using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Application.Plugins;

/// <summary>Scoped: makes contexts that act for whoever the scope acts for (the signed-in user, or a module).</summary>
public sealed class PluginContextFactory(IMediator mediator, ICurrentUser currentUser, PrinterEventStream stream)
{
	public PluginContext Create(string moduleId) => new(moduleId, mediator, currentUser, stream);
}
