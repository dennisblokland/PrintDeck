using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Queries;

/// <summary>True until the first admin account exists.</summary>
public sealed record IsSetupRequiredQuery : IQuery<bool>, IAllowAnonymous;
