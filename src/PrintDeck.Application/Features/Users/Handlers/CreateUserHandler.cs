using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Handlers;

public sealed class CreateUserHandler(IUserAccounts accounts) : ICommandHandler<CreateUserCommand, Result>
{
	public async ValueTask<Result> Handle(CreateUserCommand c, CancellationToken ct) =>
		await accounts.CreateAsync(c.UserName.Trim(), c.Password, c.Role, ct);
}
