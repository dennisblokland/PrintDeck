using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Timelapse.Dtos;
using PrintDeck.Application.Features.Timelapse.Queries;
using PrintDeck.Application.Timelapse;

namespace PrintDeck.Application.Features.Timelapse.Handlers;

public sealed class GetTimelapsesHandler(TimelapseRecorder recorder, ITimelapseStore store)
	: IQueryHandler<GetTimelapsesQuery, TimelapsesDto>
{
	public ValueTask<TimelapsesDto> Handle(GetTimelapsesQuery q, CancellationToken ct) =>
		ValueTask.FromResult(new TimelapsesDto(recorder.Status, store.List()));
}
