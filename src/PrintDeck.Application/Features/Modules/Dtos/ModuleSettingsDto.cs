using System.Text.Json.Nodes;

namespace PrintDeck.Application.Features.Modules.Dtos;

/// <param name="Settings">The module's settings without its secrets.</param>
/// <param name="SavedSecrets">Property names of the secrets that have a saved value.</param>
public sealed record ModuleSettingsDto(bool Enabled, JsonObject Settings, IReadOnlySet<string> SavedSecrets);
