using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;

namespace PrintDeck.Application.Features.ApiKeys.Commands;

/// <summary>
/// An app asks for its own key (OctoPrint's app-key flow). Needs no sign-in: the app has no key yet, and an admin
/// decides in PrintDeck. The result is the request, whose token the app polls with.
/// </summary>
public sealed record RequestAppKeyCommand(string App, string? User) : ICommand<Result<AppKeyRequest>>, IAllowAnonymous;
