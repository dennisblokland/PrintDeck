using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Handlers;

public sealed class DeleteUserHandler(IUserAccounts accounts, ICurrentUser currentUser) : ICommandHandler<DeleteUserCommand, Result>
{
	public async ValueTask<Result> Handle(DeleteUserCommand c, CancellationToken ct)
	{
		CurrentUserInfo? me = await currentUser.GetAsync();
		if (me is not null && string.Equals(me.UserName, c.UserName, StringComparison.OrdinalIgnoreCase))
			return Result.Failure("You can't delete your own account.");

		IReadOnlyList<UserAccount> users = await accounts.ListAsync(ct);
		if (UserRules.Find(users, c.UserName) is not { } user) return Result.Failure($"No user named {c.UserName}.");
		if (user.Role == UserRole.Admin && UserRules.AdminCount(users) == 1)
			return Result.Failure("PrintDeck needs at least one admin.");
		return await accounts.DeleteAsync(user.UserName, ct);
	}
}
