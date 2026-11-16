using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class ListGcodeFilesHandler(IGcodeFileStore files) : IQueryHandler<ListGcodeFilesQuery, IReadOnlyList<GcodeFile>>
{
	public async ValueTask<IReadOnlyList<GcodeFile>> Handle(ListGcodeFilesQuery q, CancellationToken ct) =>
		FileListView.Apply(await files.ListAsync(ct), q.Search, q.Sort, q.HidePrinted);
}
