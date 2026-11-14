using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Commands;

/// <summary>An admin sets someone's password (forgotten password); also lifts a lockout.</summary>
public sealed record ResetUserPasswordCommand(string UserName, string NewPassword) : ICommand<Result>, IAccountAdministration;
