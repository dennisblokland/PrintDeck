using Mediator;

using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Queries;

/// <summary>One stored file; null when it does not exist.</summary>
public sealed record GetGcodeFileQuery(string Name) : IQuery<GcodeFile?>;
