using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Handlers;

public sealed class IsSetupRequiredHandler(IUserAccounts accounts) : IQueryHandler<IsSetupRequiredQuery, bool>
{
	public async ValueTask<bool> Handle(IsSetupRequiredQuery q, CancellationToken ct) => !await accounts.AnyAsync(ct);
}
