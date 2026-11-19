using Mediator;

using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class ApproveAppKeyRequestHandler(
	AppKeyRequests requests, IApiKeys keys, ICurrentUser currentUser, PrinterEventHub events, ILogger<ApproveAppKeyRequestHandler> logger)
	: ICommandHandler<ApproveAppKeyRequestCommand, Result>
{
	public const string Gone = "That request has expired or was already answered.";

	public async ValueTask<Result> Handle(ApproveAppKeyRequestCommand c, CancellationToken ct)
	{
		if (requests.FindPending(c.Token) is not { } request) return Result.Failure(Gone);
		if (await currentUser.GetAsync() is not { IsApiKey: false } me) return Result.Failure("Sign in with an admin account to allow app keys.");

		// The key acts as the account the app asked for, so a viewer's key can watch but not control.
		string owner = request.User ?? me.UserName;
		IReadOnlyList<ApiKeyInfo> existing = await keys.ListAsync(ct);
		string name = ApiKeySecret.FreeName(request.App, existing.Select(k => k.Name).ToList());
		if (await keys.CreateAsync(owner, name, ct) is not { } key)
			return Result.Failure($"There is no account named {owner}. Ask the app to use an existing one.");

		if (!requests.Grant(c.Token, key))
		{
			// Expired between the two steps: don't leave a key nobody can pick up.
			if ((await keys.ListAsync(ct)).FirstOrDefault(k => k.Name == name) is { } made) await keys.RevokeAsync(made.Id, ct);
			return Result.Failure(Gone);
		}

		logger.LogInformation("{Admin} allowed the app key {Name} for {Owner}", me.UserName, name, owner);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
