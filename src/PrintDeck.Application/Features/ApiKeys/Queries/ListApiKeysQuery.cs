using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Queries;

public sealed record ListApiKeysQuery : IQuery<IReadOnlyList<ApiKeyInfo>>, IAccountAdministration;
