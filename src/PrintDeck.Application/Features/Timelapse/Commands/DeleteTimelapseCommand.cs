using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Timelapse.Commands;

public sealed record DeleteTimelapseCommand(string Name) : ICommand<Result>;
