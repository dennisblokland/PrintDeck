using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class DenyAppKeyRequestHandler(AppKeyRequests requests, PrinterEventHub events) : ICommandHandler<DenyAppKeyRequestCommand, Result>
{
	public ValueTask<Result> Handle(DenyAppKeyRequestCommand c, CancellationToken ct)
	{
		if (!requests.Deny(c.Token)) return ValueTask.FromResult(Result.Failure(ApproveAppKeyRequestHandler.Gone));
		events.Publish(PrinterChange.Settings);
		return ValueTask.FromResult(Result.Success());
	}
}
