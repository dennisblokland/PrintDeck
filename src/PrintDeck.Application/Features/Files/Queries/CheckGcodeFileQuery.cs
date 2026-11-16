using Mediator;

using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Queries;

/// <summary>Known problems in a file for the active printer profile; null when the file does not exist.</summary>
public sealed record CheckGcodeFileQuery(string FileName) : IQuery<IReadOnlyList<GcodeProblem>?>;
