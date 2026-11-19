using Mediator;

using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Features.ApiKeys.Dtos;
using PrintDeck.Application.Features.ApiKeys.Queries;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class PollAppKeyRequestHandler(AppKeyRequests requests) : IQueryHandler<PollAppKeyRequestQuery, AppKeyPoll>
{
	public ValueTask<AppKeyPoll> Handle(PollAppKeyRequestQuery q, CancellationToken ct)
	{
		(AppKeyRequestState state, string? key) = requests.Poll(q.Token ?? "");
		return ValueTask.FromResult(new AppKeyPoll(state, key));
	}
}
