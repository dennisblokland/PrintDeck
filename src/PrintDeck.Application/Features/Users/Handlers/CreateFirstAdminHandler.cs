using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Handlers;

public sealed class CreateFirstAdminHandler(IUserAccounts accounts) : ICommandHandler<CreateFirstAdminCommand, Result>
{
	// Two setup forms submitted at once must not both create an admin.
	private static readonly SemaphoreSlim _gate = new(1, 1);

	public async ValueTask<Result> Handle(CreateFirstAdminCommand c, CancellationToken ct)
	{
		await _gate.WaitAsync(ct);
		try
		{
			if (await accounts.AnyAsync(ct)) return Result.Failure("PrintDeck is already set up. Sign in instead.");
			return await accounts.CreateAsync(c.UserName.Trim(), c.Password, UserRole.Admin, ct);
		}
		finally { _gate.Release(); }
	}
}
