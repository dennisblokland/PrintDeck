using System.Text.Json.Nodes;

namespace PrintDeck.Domain.Settings;

/// <summary>One module's part of <c>settings.json</c>.</summary>
/// <param name="Settings">The module's own settings as it saved them; null until it saves any.</param>
public sealed record ModuleSection(bool Enabled = false, JsonObject? Settings = null);
