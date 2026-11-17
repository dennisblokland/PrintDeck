using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Settings.Commands;

/// <summary>A new shared key, which also switches it back on. Admin accounts only: a key can't replace itself.</summary>
public sealed record RegenerateApiKeyCommand : ICommand<Result<string>>, IAccountAdministration;
