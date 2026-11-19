using Mediator;

using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Queries;

/// <summary>App-key requests waiting for an admin.</summary>
public sealed record ListAppKeyRequestsQuery : IQuery<IReadOnlyList<AppKeyRequest>>;
