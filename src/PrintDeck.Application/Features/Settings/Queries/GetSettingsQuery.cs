using Mediator;

using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Queries;

/// <summary>All settings: saved printer profiles, the active one, presets, camera, API key.</summary>
public sealed record GetSettingsQuery : IQuery<HostSettings>;
