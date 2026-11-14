using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Commands;

public sealed record CreateUserCommand(string UserName, string Password, UserRole Role) : ICommand<Result>, IAccountAdministration;
