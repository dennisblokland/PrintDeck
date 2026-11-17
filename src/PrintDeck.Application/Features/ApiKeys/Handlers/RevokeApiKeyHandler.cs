using Mediator;

using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class RevokeApiKeyHandler(IApiKeys keys, ICurrentUser currentUser, PrinterEventHub events, ILogger<RevokeApiKeyHandler> logger)
	: ICommandHandler<RevokeApiKeyCommand, Result>
{
	public async ValueTask<Result> Handle(RevokeApiKeyCommand c, CancellationToken ct)
	{
		ApiKeyInfo? key = (await keys.ListAsync(ct)).FirstOrDefault(k => k.Id == c.Id);
		if (key is null || !await keys.RevokeAsync(c.Id, ct)) return Result.Failure("That key no longer exists.");

		logger.LogInformation("{User} revoked the app key {Name}", (await currentUser.GetAsync())?.UserName ?? "unknown", key.Name);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
