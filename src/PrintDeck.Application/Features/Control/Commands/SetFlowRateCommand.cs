using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Control.Commands;

/// <summary>Extrusion factor, in percent (<c>M221</c>); works while printing.</summary>
public sealed record SetFlowRateCommand(int Percent) : ICommand<Result>;
