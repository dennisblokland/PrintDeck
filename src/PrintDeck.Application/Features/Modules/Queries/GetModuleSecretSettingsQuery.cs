using System.Text.Json.Nodes;

using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Modules.Queries;

/// <summary>A module's settings with its secrets, for the module's own context only.</summary>
public sealed record GetModuleSecretSettingsQuery(string ModuleId) : IQuery<Result<JsonObject>>, IModuleOnly;
