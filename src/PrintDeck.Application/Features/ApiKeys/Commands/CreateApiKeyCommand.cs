using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.ApiKeys.Commands;

/// <summary>Makes an app key that acts as the signed-in admin; the result is the key, shown once.</summary>
public sealed record CreateApiKeyCommand(string Name) : ICommand<Result<string>>, IAccountAdministration;
