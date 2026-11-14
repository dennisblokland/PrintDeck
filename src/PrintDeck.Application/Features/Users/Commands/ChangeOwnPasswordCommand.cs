using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users.Commands;

/// <summary>The signed-in user changes their own password.</summary>
public sealed record ChangeOwnPasswordCommand(string CurrentPassword, string NewPassword) : ICommand<Result>, IAllowAnyUser;
