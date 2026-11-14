using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class GetGcodeToolpathHandler(IGcodeFileStore files, ToolpathCache cache)
	: IQueryHandler<GetGcodeToolpathQuery, ToolpathResult?>
{
	public async ValueTask<ToolpathResult?> Handle(GetGcodeToolpathQuery q, CancellationToken ct)
	{
		GcodeFile? file = await files.GetAsync(q.FileName, ct);
		if (file is null) return null;
		GcodeToolpath toolpath = await cache.GetAsync(file, () => files.OpenRead(file.Name), ct);
		return new ToolpathResult(file, toolpath);
	}
}
