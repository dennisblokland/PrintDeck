namespace PrintDeck.Plugins;

/// <param name="IsAdmin">May change things: control the printer, change settings.</param>
/// <param name="IsModule">The module itself, outside any request.</param>
public sealed record PluginUser(string Name, bool IsAdmin, bool IsModule);
