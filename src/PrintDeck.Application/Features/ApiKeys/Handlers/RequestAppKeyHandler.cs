using Mediator;

using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class RequestAppKeyHandler(AppKeyRequests requests, PrinterEventHub events, ILogger<RequestAppKeyHandler> logger)
	: ICommandHandler<RequestAppKeyCommand, Result<AppKeyRequest>>
{
	public ValueTask<Result<AppKeyRequest>> Handle(RequestAppKeyCommand c, CancellationToken ct)
	{
		Result<AppKeyRequest> added = requests.Add(c.App, c.User);
		if (added.IsSuccess)
		{
			logger.LogInformation("{App} asks for an app key for {User}", added.Value!.App, added.Value.User ?? "(no user)");
			events.Publish(PrinterChange.Settings); // admins see it at once
		}
		return ValueTask.FromResult(added);
	}
}
