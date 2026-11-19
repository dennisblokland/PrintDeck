using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.ApiKeys.Commands;

/// <summary>Allows an app-key request: makes a key for the account the app named (or the admin allowing it).</summary>
public sealed record ApproveAppKeyRequestCommand(string Token) : ICommand<Result>, IAccountAdministration;
