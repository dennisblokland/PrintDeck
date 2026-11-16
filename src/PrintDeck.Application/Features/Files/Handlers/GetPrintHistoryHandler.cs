using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Dtos;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class GetPrintHistoryHandler(IGcodeFileStore files) : IQueryHandler<GetPrintHistoryQuery, PrintHistoryDto>
{
	public async ValueTask<PrintHistoryDto> Handle(GetPrintHistoryQuery q, CancellationToken ct)
	{
		IReadOnlyList<PrintHistoryEntry> all = await files.ListPrintsAsync(ct);
		List<PrintHistoryEntry> recent = all
			.OrderByDescending(e => e.Print.EndedAt)
			.Take(Math.Max(0, q.Limit))
			.ToList();
		return new PrintHistoryDto(recent, PrintStats.From(all.Select(e => e.Print)));
	}
}
