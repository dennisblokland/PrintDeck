using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Queries;

public sealed record ListUsersQuery : IQuery<IReadOnlyList<UserAccount>>, IAccountAdministration;
