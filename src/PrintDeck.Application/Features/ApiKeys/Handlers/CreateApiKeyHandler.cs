using Mediator;

using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class CreateApiKeyHandler(IApiKeys keys, ICurrentUser currentUser, PrinterEventHub events, ILogger<CreateApiKeyHandler> logger)
	: ICommandHandler<CreateApiKeyCommand, Result<string>>
{
	public async ValueTask<Result<string>> Handle(CreateApiKeyCommand c, CancellationToken ct)
	{
		if (await currentUser.GetAsync() is not { IsApiKey: false } me)
			return Result.Failure<string>("Sign in with an admin account to make keys.");

		IReadOnlyList<ApiKeyInfo> existing = await keys.ListAsync(ct);
		if (ApiKeySecret.NameProblem(c.Name, existing.Select(k => k.Name)) is { } problem) return Result.Failure<string>(problem);

		string name = c.Name.Trim();
		if (await keys.CreateAsync(me.UserName, name, ct) is not { } key) return Result.Failure<string>($"No user named {me.UserName}.");

		logger.LogInformation("{User} made the app key {Name}", me.UserName, name);
		events.Publish(PrinterChange.Settings);
		return Result.Success(key);
	}
}
