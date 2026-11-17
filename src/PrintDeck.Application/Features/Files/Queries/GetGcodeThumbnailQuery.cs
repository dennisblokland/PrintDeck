using Mediator;

using PrintDeck.Application.Features.Files.Dtos;

namespace PrintDeck.Application.Features.Files.Queries;

/// <summary>The slicer thumbnail of a stored file; null when the file or its thumbnail does not exist.</summary>
public sealed record GetGcodeThumbnailQuery(string Name) : IQuery<ThumbnailContent?>;
