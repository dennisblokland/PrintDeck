using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.ApiKeys.Commands;

/// <summary>Stops the shared key from signing in, so only app keys work. <see cref="Settings.Commands.RegenerateApiKeyCommand"/> turns it back on with a new key.</summary>
public sealed record DisableSharedApiKeyCommand : ICommand<Result>, IAccountAdministration;
