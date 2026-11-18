using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Health.Dtos;
using PrintDeck.Application.Features.Health.Queries;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Health;

namespace PrintDeck.Application.Features.Health.Handlers;

public sealed class GetHealthHandler(IHostHealth host, IPrinterSession session) : IQueryHandler<GetHealthQuery, HealthReport>
{
	public async ValueTask<HealthReport> Handle(GetHealthQuery q, CancellationToken ct)
	{
		DiskSpace? disk = host.DataDisk();
		PiThrottling? pi = await host.PiThrottlingAsync(ct);
		string? firmware = session.Firmware;

		List<HealthWarning> warnings = [];
		if (disk?.Check() is { } full) warnings.Add(full);
		if (pi is not null) warnings.AddRange(pi.Check());
		warnings.AddRange(FirmwareCheck.Check(firmware));

		return new HealthReport(disk, pi, firmware, [.. warnings.OrderByDescending(w => w.Severity)]);
	}
}
