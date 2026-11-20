using Mediator;

using PrintDeck.Application.Features.Modules.Dtos;

namespace PrintDeck.Application.Features.Modules.Queries;

/// <summary>A module's settings for a page: never its secrets. Null for an unknown module.</summary>
public sealed record GetModuleSettingsQuery(string ModuleId) : IQuery<ModuleSettingsDto?>;
