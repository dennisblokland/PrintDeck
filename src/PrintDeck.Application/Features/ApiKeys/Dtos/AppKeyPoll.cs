using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.ApiKeys.Dtos;

/// <summary>The answer to an app polling its request; <paramref name="Key"/> only when granted.</summary>
public sealed record AppKeyPoll(AppKeyRequestState State, string? Key);
