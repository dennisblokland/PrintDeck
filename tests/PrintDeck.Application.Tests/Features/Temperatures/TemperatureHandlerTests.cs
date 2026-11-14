using PrintDeck.Application.Features.Temperatures.Commands;
using PrintDeck.Application.Features.Temperatures.Handlers;
using PrintDeck.Application.Features.Temperatures.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Tests.Features.Temperatures;

public class TemperatureHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public class SetTemperature : TemperatureHandlerTests
	{
		[Theory]
		[InlineData("T0", 210, "M104 T0 S210")]
		[InlineData("T1", 0, "M104 T1 S0")]
		[InlineData("T2", 320, "M104 T2 S320")]
		[InlineData("T0", 210.6, "M104 T0 S211")]
		[InlineData("B", 60, "M140 S60")]
		[InlineData("B", 130, "M140 S130")]
		[InlineData("C", 40, "M141 S40")]
		[InlineData("B", 200, null)]
		[InlineData("B", -1, null)]
		[InlineData("C", 81, null)]
		[InlineData("T0", 400, null)]
		[InlineData("T", 200, null)]
		[InlineData("Tx", 200, null)]
		[InlineData("X", 10, null)]
		public void Builds_the_heater_command(string heater, double target, string? expected) =>
			Assert.Equal(expected, SetTemperatureHandler.Build(new SetTemperatureCommand(heater, target)));

		[Fact]
		public async Task Sends_a_valid_target()
		{
			Assert.True((await new SetTemperatureHandler(_session).Handle(new SetTemperatureCommand("B", 60), Ct)).IsSuccess);
			Assert.Equal(["M140 S60"], _session.Enqueued);
		}

		[Fact]
		public async Task Explains_an_invalid_target()
		{
			Result r = await new SetTemperatureHandler(_session).Handle(new SetTemperatureCommand("T0", 400), Ct);
			Assert.Equal("Invalid target 400°C for T0.", r.Error);
			Assert.Empty(_session.Enqueued);
		}
	}

	public class ApplyPreset : TemperatureHandlerTests
	{
		private ValueTask<Result> Apply(string preset, PrinterProfile? profile = null) =>
			new ApplyTemperaturePresetHandler(_session, new FakeSettingsStore(profile)).Handle(new ApplyTemperaturePresetCommand(preset), Ct);

		[Fact]
		public async Task Heats_hotend_and_bed()
		{
			Assert.True((await Apply("PLA")).IsSuccess);
			Assert.Equal(["M104 T0 S205", "M140 S60"], _session.Enqueued);
		}

		[Fact]
		public async Task Heats_every_extruder()
		{
			await Apply("PETG", new PrinterProfile { ExtruderCount = 2 });
			Assert.Equal(["M104 T0 S240", "M104 T1 S240", "M140 S80"], _session.Enqueued);
		}

		[Fact]
		public async Task Skips_the_bed_when_it_is_not_heated()
		{
			await Apply("ABS", new PrinterProfile { HasHeatedBed = false });
			Assert.Equal(["M104 T0 S250"], _session.Enqueued);
		}

		[Fact]
		public async Task Off_cools_everything()
		{
			await Apply("Off", new PrinterProfile { ExtruderCount = 2 });
			Assert.Equal(["M104 T0 S0", "M104 T1 S0", "M140 S0"], _session.Enqueued);
		}

		[Fact]
		public async Task Rejects_an_unknown_preset()
		{
			Assert.Equal("Unknown preset TPU.", (await Apply("TPU")).Error);
			Assert.Empty(_session.Enqueued);
		}
	}

	public class History : TemperatureHandlerTests
	{
		[Fact]
		public async Task Returns_samples_within_the_window()
		{
			DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
			_session.Temperatures.Record(new TemperatureSample(now.AddMinutes(-20), [new HeaterReading("T0", 20, 0)]));
			_session.Temperatures.Record(new TemperatureSample(now.AddMinutes(-2), [new HeaterReading("T0", 30, 0)]));

			IReadOnlyList<TemperatureSample> samples = await new GetTemperatureHistoryHandler(_session, new ManualTimeProvider(now))
				.Handle(new GetTemperatureHistoryQuery(TimeSpan.FromMinutes(10)), Ct);

			Assert.Equal(30, Assert.Single(samples).Heaters[0].Actual);
		}
	}
}
