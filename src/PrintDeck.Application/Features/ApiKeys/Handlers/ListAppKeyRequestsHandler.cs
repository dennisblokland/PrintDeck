using Mediator;

using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Features.ApiKeys.Queries;
using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class ListAppKeyRequestsHandler(AppKeyRequests requests) : IQueryHandler<ListAppKeyRequestsQuery, IReadOnlyList<AppKeyRequest>>
{
	public ValueTask<IReadOnlyList<AppKeyRequest>> Handle(ListAppKeyRequestsQuery q, CancellationToken ct) =>
		ValueTask.FromResult(requests.Pending());
}
