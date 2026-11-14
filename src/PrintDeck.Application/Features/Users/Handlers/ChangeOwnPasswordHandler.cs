using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Handlers;

public sealed class ChangeOwnPasswordHandler(IUserAccounts accounts, ICurrentUser currentUser) : ICommandHandler<ChangeOwnPasswordCommand, Result>
{
	public async ValueTask<Result> Handle(ChangeOwnPasswordCommand c, CancellationToken ct)
	{
		CurrentUserInfo? me = await currentUser.GetAsync();
		if (me is null || me.IsApiKey) return Result.Failure("Sign in with your own account to change its password.");
		return await accounts.ChangePasswordAsync(me.UserName, c.CurrentPassword, c.NewPassword, ct);
	}
}
