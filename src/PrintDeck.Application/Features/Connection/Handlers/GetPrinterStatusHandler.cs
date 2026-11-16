using Mediator;

using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Application.Features.Connection.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Features.Connection.Handlers;

public sealed class GetPrinterStatusHandler(IPrinterSession session, TimeProvider time)
	: IQueryHandler<GetPrinterStatusQuery, PrinterStatusDto>
{
	public ValueTask<PrinterStatusDto> Handle(GetPrinterStatusQuery query, CancellationToken ct)
	{
		DateTimeOffset now = time.GetUtcNow();
		JobDto? job = session.CurrentJob is { } j
			? new JobDto(j.FileName, j.Status, j.Progress, j.Elapsed(now), j.EstimatedRemaining(now), j.LinesSent, j.StartedAt, j.Kind)
			: null;

		return ValueTask.FromResult(new PrinterStatusDto(
			session.State,
			session.Port,
			session.BaudRate,
			session.Firmware,
			session.ErrorMessage,
			job,
			session.Temperatures.Latest?.Heaters ?? []));
	}
}
