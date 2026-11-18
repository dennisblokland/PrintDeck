using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Queries;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class ListFoldersHandler(IGcodeFileStore files) : IQueryHandler<ListFoldersQuery, IReadOnlyList<string>>
{
	public ValueTask<IReadOnlyList<string>> Handle(ListFoldersQuery q, CancellationToken ct) => ValueTask.FromResult(files.ListFolders());
}
