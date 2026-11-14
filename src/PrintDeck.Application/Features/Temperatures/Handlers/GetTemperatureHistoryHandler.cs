using System.Globalization;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Temperatures.Commands;
using PrintDeck.Application.Features.Temperatures.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Features.Temperatures.Handlers;

public sealed class GetTemperatureHistoryHandler(IPrinterSession session, TimeProvider time)
	: IQueryHandler<GetTemperatureHistoryQuery, IReadOnlyList<TemperatureSample>>
{
	public ValueTask<IReadOnlyList<TemperatureSample>> Handle(GetTemperatureHistoryQuery q, CancellationToken ct) =>
		ValueTask.FromResult(session.Temperatures.Since(q.Window, time.GetUtcNow()));
}
