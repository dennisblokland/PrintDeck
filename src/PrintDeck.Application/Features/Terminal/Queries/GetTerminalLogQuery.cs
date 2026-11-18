using Mediator;

using PrintDeck.Domain.Terminal;

namespace PrintDeck.Application.Features.Terminal.Queries;

/// <summary>The newest terminal lines; with <paramref name="Filtered"/>, without the lines the enabled terminal filters hide.</summary>
public sealed record GetTerminalLogQuery(int Last = 500, bool Filtered = true) : IQuery<IReadOnlyList<TerminalEntry>>;
