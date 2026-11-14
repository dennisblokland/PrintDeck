using Mediator;

using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Application.Features.Terminal.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Terminal;
using PrintDeck.Domain.Terminal.Enums;

namespace PrintDeck.Application.Features.Terminal.Handlers;

public sealed class GetTerminalLogHandler(IPrinterSession session) : IQueryHandler<GetTerminalLogQuery, IReadOnlyList<TerminalEntry>>
{
	public ValueTask<IReadOnlyList<TerminalEntry>> Handle(GetTerminalLogQuery q, CancellationToken ct)
	{
		IEnumerable<TerminalEntry> entries = session.Terminal(q.HideTemperature ? q.Last * 4 : q.Last);
		if (q.HideTemperature)
			entries = entries.Where(e => !e.IsTemperatureChatter && !(e.Direction == TerminalDirection.Received && e.Text == "ok"));
		return ValueTask.FromResult<IReadOnlyList<TerminalEntry>>(entries.TakeLast(q.Last).ToList());
	}
}
