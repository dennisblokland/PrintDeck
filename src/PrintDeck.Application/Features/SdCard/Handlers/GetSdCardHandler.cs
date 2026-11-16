using Mediator;

using PrintDeck.Application.Features.SdCard.Dtos;
using PrintDeck.Application.Features.SdCard.Queries;
using PrintDeck.Application.Printer.Interfaces;

namespace PrintDeck.Application.Features.SdCard.Handlers;

public sealed class GetSdCardHandler(IPrinterSession session) : IQueryHandler<GetSdCardQuery, SdCardDto>
{
	public ValueTask<SdCardDto> Handle(GetSdCardQuery query, CancellationToken ct) =>
		ValueTask.FromResult(new SdCardDto(session.SdCard, session.SdFiles));
}
