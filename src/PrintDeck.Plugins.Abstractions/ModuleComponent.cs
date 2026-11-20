using PrintDeck.Plugins.Enums;

namespace PrintDeck.Plugins;

/// <summary>
/// A Blazor component a module adds to one of PrintDeck's pages. PrintDeck sets its <c>Context</c> parameter
/// (an <see cref="IPluginContext"/>) for the signed-in user; the component decides what a viewer may see.
/// </summary>
public sealed record ModuleComponent(ModuleSlot Slot, Type ComponentType);
