using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Handlers;

public sealed class ResetUserPasswordHandler(IUserAccounts accounts) : ICommandHandler<ResetUserPasswordCommand, Result>
{
	public async ValueTask<Result> Handle(ResetUserPasswordCommand c, CancellationToken ct) =>
		await accounts.ResetPasswordAsync(c.UserName, c.NewPassword, ct);
}
