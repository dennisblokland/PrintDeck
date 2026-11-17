using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Dtos;

/// <summary>An open thumbnail image; the caller disposes <see cref="Content"/>. <see cref="File"/> dates it for caching.</summary>
public sealed record ThumbnailContent(Stream Content, string ContentType, GcodeFile File);
