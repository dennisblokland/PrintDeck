using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.ApiKeys.Commands;

public sealed record DenyAppKeyRequestCommand(string Token) : ICommand<Result>, IAccountAdministration;
