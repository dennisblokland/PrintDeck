using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Timelapse.Queries;
using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Application.Features.Timelapse.Handlers;

public sealed class GetTimelapseVideoHandler(ITimelapseStore store) : IQueryHandler<GetTimelapseVideoQuery, Stream?>
{
	public ValueTask<Stream?> Handle(GetTimelapseVideoQuery q, CancellationToken ct) =>
		ValueTask.FromResult(TimelapseName.IsValid(q.Name) ? store.OpenRead(q.Name) : null);
}
