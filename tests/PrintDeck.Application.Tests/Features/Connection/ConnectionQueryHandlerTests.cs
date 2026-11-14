using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Application.Features.Connection.Handlers;
using PrintDeck.Application.Features.Connection.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Tests.Features.Connection;

public class ConnectionQueryHandlerTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
	private readonly FakePrinterSession _session = new();
	private readonly ManualTimeProvider _time = new(_t0);
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Status_reports_an_idle_printer()
	{
		_session.State = PrinterState.Offline;

		PrinterStatusDto s = await new GetPrinterStatusHandler(_session, _time).Handle(new GetPrinterStatusQuery(), Ct);

		Assert.Equal(PrinterState.Offline, s.State);
		Assert.Null(s.Job);
		Assert.Empty(s.Heaters);
	}

	[Fact]
	public async Task Status_maps_connection_job_and_temperatures()
	{
		_session.State = PrinterState.Printing;
		_session.Port = "VIRTUAL";
		_session.BaudRate = 115200;
		_session.Firmware = "Marlin";
		_session.ReportTemperatures(new HeaterReading("T0", 200, 210));
		PrintJob job = new("cube.gcode", 1000, TimeSpan.FromMinutes(20), _t0);
		job.Advance(500);
		_session.CurrentJob = job;
		_time.Advance(TimeSpan.FromMinutes(10));

		PrinterStatusDto s = await new GetPrinterStatusHandler(_session, _time).Handle(new GetPrinterStatusQuery(), Ct);

		Assert.Equal(("VIRTUAL", 115200, "Marlin"), (s.Port, s.BaudRate, s.Firmware));
		Assert.Equal("T0", Assert.Single(s.Heaters).Heater);
		JobDto dto = Assert.IsType<JobDto>(s.Job);
		Assert.Equal("cube.gcode", dto.FileName);
		Assert.Equal(JobStatus.Printing, dto.Status);
		Assert.Equal(0.5, dto.Progress);
		Assert.Equal(TimeSpan.FromMinutes(10), dto.Elapsed);
		Assert.Equal(job.EstimatedRemaining(_time.Now), dto.Remaining);
		Assert.Equal(1, dto.LinesSent);
	}

	[Fact]
	public async Task Disconnect_closes_the_session()
	{
		Assert.True((await new DisconnectPrinterHandler(_session).Handle(new DisconnectPrinterCommand(), Ct)).IsSuccess);
		Assert.True(_session.Disconnected);
	}

	[Fact]
	public async Task Lists_discovered_ports()
	{
		IReadOnlyList<string> ports = await new ListPortsHandler(new StubPorts()).Handle(new ListPortsQuery(), Ct);
		Assert.Equal(["/dev/ttyUSB0", "VIRTUAL"], ports);
	}

	private sealed class StubPorts : IPortDiscovery
	{
		public IReadOnlyList<string> ListPorts() => ["/dev/ttyUSB0", "VIRTUAL"];
	}
}
