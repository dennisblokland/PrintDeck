using Mediator;

using PrintDeck.Application.Features.Modules.Dtos;

namespace PrintDeck.Application.Features.Modules.Queries;

/// <summary>Every module PrintDeck was built with, by name, and whether it is on.</summary>
public sealed record ListModulesQuery : IQuery<IReadOnlyList<ModuleDto>>;
