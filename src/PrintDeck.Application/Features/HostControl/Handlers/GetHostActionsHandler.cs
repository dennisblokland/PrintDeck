using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.HostControl.Queries;
using PrintDeck.Domain.HostControl.Enums;

namespace PrintDeck.Application.Features.HostControl.Handlers;

public sealed class GetHostActionsHandler(IHostCommands commands) : IQueryHandler<GetHostActionsQuery, IReadOnlyList<HostAction>>
{
	public ValueTask<IReadOnlyList<HostAction>> Handle(GetHostActionsQuery query, CancellationToken ct) =>
		ValueTask.FromResult(commands.Available);
}
