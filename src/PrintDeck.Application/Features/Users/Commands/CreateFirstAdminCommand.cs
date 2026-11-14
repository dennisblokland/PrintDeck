using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Commands;

/// <summary>First-run setup: creates the first admin. Refused once any account exists.</summary>
public sealed record CreateFirstAdminCommand(string UserName, string Password) : ICommand<Result>, IAllowAnonymous;
