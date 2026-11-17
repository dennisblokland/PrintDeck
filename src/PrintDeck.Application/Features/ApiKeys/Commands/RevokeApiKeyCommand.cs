using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.ApiKeys.Commands;

/// <summary>Deletes an app key; the slicer using it is refused from its next request.</summary>
public sealed record RevokeApiKeyCommand(string Id) : ICommand<Result>, IAccountAdministration;
