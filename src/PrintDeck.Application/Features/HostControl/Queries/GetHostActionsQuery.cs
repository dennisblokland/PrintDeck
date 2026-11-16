using Mediator;

using PrintDeck.Domain.HostControl.Enums;

namespace PrintDeck.Application.Features.HostControl.Queries;

/// <summary>The System menu entries that have a command configured.</summary>
public sealed record GetHostActionsQuery : IQuery<IReadOnlyList<HostAction>>;
