using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Control.Commands;

/// <summary>Sends custom button <paramref name="Index"/>; <paramref name="Label"/> guards against a page showing an old list.</summary>
public sealed record RunCustomButtonCommand(int Index, string Label) : ICommand<Result>;
