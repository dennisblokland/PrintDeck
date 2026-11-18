using PrintDeck.Application.Features.Health.Dtos;
using PrintDeck.Application.Features.Health.Handlers;
using PrintDeck.Application.Features.Health.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Health;

namespace PrintDeck.Application.Tests.Features.Health;

public class GetHealthHandlerTests
{
	private readonly FakeHostHealth _host = new();
	private readonly FakePrinterSession _session = new();

	private ValueTask<HealthReport> Get() =>
		new GetHealthHandler(_host, _session).Handle(new GetHealthQuery(), TestContext.Current.CancellationToken);

	[Fact]
	public async Task A_healthy_host_has_no_warnings()
	{
		_session.Firmware = "Marlin 2.1.2";

		HealthReport r = await Get();

		Assert.Empty(r.Warnings);
		Assert.Equal(20L << 30, r.Disk!.FreeBytes);
		Assert.Null(r.Pi);
		Assert.Equal("Marlin 2.1.2", r.Firmware);
	}

	[Fact]
	public async Task Collects_disk_pi_and_firmware_warnings_errors_first()
	{
		_host.Disk = new DiskSpace(500L << 20, 32L << 30);
		_host.Pi = new PiThrottling(0x1);
		_session.Firmware = "Marlin 1.0.2";

		HealthReport r = await Get();

		Assert.Equal(["pi-undervoltage", "disk", "firmware-thermal-runaway"], r.Warnings.Select(w => w.Id));
	}

	[Fact]
	public async Task Copes_without_disk_figures_or_a_printer()
	{
		_host.Disk = null;

		HealthReport r = await Get();

		Assert.Empty(r.Warnings);
		Assert.Null(r.Firmware);
	}
}
