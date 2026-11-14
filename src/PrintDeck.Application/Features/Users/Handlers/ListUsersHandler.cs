using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Handlers;

public sealed class ListUsersHandler(IUserAccounts accounts) : IQueryHandler<ListUsersQuery, IReadOnlyList<UserAccount>>
{
	public ValueTask<IReadOnlyList<UserAccount>> Handle(ListUsersQuery q, CancellationToken ct) => new(accounts.ListAsync(ct));
}
