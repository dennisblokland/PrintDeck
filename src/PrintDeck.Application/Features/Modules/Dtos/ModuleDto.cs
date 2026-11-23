using PrintDeck.Plugins;

namespace PrintDeck.Application.Features.Modules.Dtos;

/// <param name="Enabled">Switched on in the settings.</param>
/// <param name="Running">Started; its components show only then.</param>
public sealed record ModuleDto(string Id, string Name, string Version, bool Enabled, bool Running, IReadOnlyList<ModuleComponent> Components);
