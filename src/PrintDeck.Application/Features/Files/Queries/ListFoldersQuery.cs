using Mediator;

namespace PrintDeck.Application.Features.Files.Queries;

/// <summary>Every folder of stored files, nested ones included ("parts", "parts/brackets"), sorted.</summary>
public sealed record ListFoldersQuery : IQuery<IReadOnlyList<string>>;
