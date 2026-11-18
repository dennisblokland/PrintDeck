using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Dtos;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class GetGcodeThumbnailHandler(IGcodeFileStore files) : IQueryHandler<GetGcodeThumbnailQuery, ThumbnailContent?>
{
	public async ValueTask<ThumbnailContent?> Handle(GetGcodeThumbnailQuery query, CancellationToken ct)
	{
		if (FilePath.Normalize(query.Name) is not { Length: > 0 } name || name != query.Name) return null;
		if (await files.GetAsync(query.Name, ct) is not { HasThumbnail: true } file) return null;
		return files.OpenThumbnail(file.Name) is { } image ? new ThumbnailContent(image.Content, image.ContentType, file) : null;
	}
}
