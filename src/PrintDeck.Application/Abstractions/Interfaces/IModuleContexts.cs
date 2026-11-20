using PrintDeck.Application.Plugins;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Makes the context a running module acts through (as itself, not as a user).</summary>
public interface IModuleContexts
{
	ModuleContextLease Create(string moduleId);
}
