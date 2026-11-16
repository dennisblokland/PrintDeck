using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Features.Connection.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.Features.Connection;

public class ConnectPrinterHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private readonly FakeSettingsStore _settings = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();

	private ValueTask<Result> Connect(string port, int baud) =>
		new ConnectPrinterHandler(_session, _settings, _events.Hub).Handle(new ConnectPrinterCommand(port, baud), TestContext.Current.CancellationToken);

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task Requires_a_port(string port)
	{
		Result r = await Connect(port, 115200);
		Assert.Equal("Choose a port.", r.Error);
		Assert.Null(_session.ConnectedWith);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(12345)]
	public async Task Rejects_unsupported_baud_rates(int baud)
	{
		Result r = await Connect("/dev/ttyUSB0", baud);
		Assert.False(r.IsSuccess);
		Assert.Null(_session.ConnectedWith);
	}

	[Fact]
	public async Task Remembers_port_and_baud_rate_after_connecting()
	{
		Result r = await Connect("/dev/ttyACM0", 250000);

		Assert.True(r.IsSuccess);
		Assert.Equal(("/dev/ttyACM0", 250000), _session.ConnectedWith);
		Assert.Equal("/dev/ttyACM0", _settings.Settings.LastPort);
		Assert.Equal(250000, _settings.Settings.DefaultBaudRate);
		Assert.Contains(PrinterChange.Settings, _events.Published);
	}

	[Fact]
	public async Task Saves_nothing_when_the_connection_fails()
	{
		_session.ConnectResult = Result.Failure("No response");

		Result r = await Connect("/dev/ttyACM0", 115200);

		Assert.Equal("No response", r.Error);
		Assert.Equal(0, _settings.Saves);
		Assert.Empty(_events.Published);
	}
}
