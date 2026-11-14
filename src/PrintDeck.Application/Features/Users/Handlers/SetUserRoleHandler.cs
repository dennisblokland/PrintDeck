using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Handlers;

public sealed class SetUserRoleHandler(IUserAccounts accounts) : ICommandHandler<SetUserRoleCommand, Result>
{
	public async ValueTask<Result> Handle(SetUserRoleCommand c, CancellationToken ct)
	{
		IReadOnlyList<UserAccount> users = await accounts.ListAsync(ct);
		if (UserRules.Find(users, c.UserName) is not { } user) return Result.Failure($"No user named {c.UserName}.");
		if (user.Role == c.Role) return Result.Success();
		if (user.Role == UserRole.Admin && UserRules.AdminCount(users) == 1)
			return Result.Failure("PrintDeck needs at least one admin. Make someone else admin first.");
		return await accounts.SetRoleAsync(user.UserName, c.Role, ct);
	}
}
