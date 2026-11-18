using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Terminal.Queries;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Terminal;

namespace PrintDeck.Application.Features.Terminal.Handlers;

public sealed class GetTerminalLogHandler(IPrinterSession session, ISettingsStore settings) : IQueryHandler<GetTerminalLogQuery, IReadOnlyList<TerminalEntry>>
{
	public async ValueTask<IReadOnlyList<TerminalEntry>> Handle(GetTerminalLogQuery q, CancellationToken ct)
	{
		List<TerminalFilter> filters = [];
		if (q.Filtered)
		{
			HostSettings s = await settings.GetAsync(ct);
			filters = s.TerminalFilters.Where(f => f.Enabled).ToList();
		}
		if (filters.Count == 0) return session.Terminal(q.Last).ToList();

		// Look further back so that hidden chatter doesn't leave only a handful of lines.
		return session.Terminal(q.Last * 4)
			.Where(e => !filters.Any(f => f.Matches(e.Text)))
			.TakeLast(q.Last)
			.ToList();
	}
}
