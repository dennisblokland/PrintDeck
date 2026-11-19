using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.ApiKeys.Dtos;

namespace PrintDeck.Application.Features.ApiKeys.Queries;

/// <summary>An app asking whether its key request was allowed; it holds only the token, so no sign-in.</summary>
public sealed record PollAppKeyRequestQuery(string Token) : IQuery<AppKeyPoll>, IAllowAnonymous;
