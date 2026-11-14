using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Commands;

public sealed record SetUserRoleCommand(string UserName, UserRole Role) : ICommand<Result>, IAccountAdministration;
