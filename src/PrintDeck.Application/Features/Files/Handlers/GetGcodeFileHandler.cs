using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class GetGcodeFileHandler(IGcodeFileStore files) : IQueryHandler<GetGcodeFileQuery, GcodeFile?>
{
	public async ValueTask<GcodeFile?> Handle(GetGcodeFileQuery query, CancellationToken ct) =>
		FilePath.Normalize(query.Name) == query.Name ? await files.GetAsync(query.Name, ct) : null;
}
