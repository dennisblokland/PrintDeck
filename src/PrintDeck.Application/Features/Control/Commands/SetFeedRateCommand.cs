using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Control.Commands;

/// <summary>Speed factor for moves, in percent (<c>M220</c>); works while printing.</summary>
public sealed record SetFeedRateCommand(int Percent) : ICommand<Result>;
