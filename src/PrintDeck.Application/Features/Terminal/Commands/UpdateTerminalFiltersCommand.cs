using Mediator;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.Terminal;

namespace PrintDeck.Application.Features.Terminal.Commands;

/// <summary>Replaces the terminal filters: their names, patterns and which ones are on.</summary>
public sealed record UpdateTerminalFiltersCommand(IReadOnlyList<TerminalFilter> Filters) : ICommand<Result>;
