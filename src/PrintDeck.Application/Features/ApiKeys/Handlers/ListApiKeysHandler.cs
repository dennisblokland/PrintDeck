using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.ApiKeys.Queries;
using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class ListApiKeysHandler(IApiKeys keys) : IQueryHandler<ListApiKeysQuery, IReadOnlyList<ApiKeyInfo>>
{
	public async ValueTask<IReadOnlyList<ApiKeyInfo>> Handle(ListApiKeysQuery query, CancellationToken ct) => await keys.ListAsync(ct);
}
