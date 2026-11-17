using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Features.Control.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Tests.Features.Control;

public class ControlHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private readonly FakeSettingsStore _settings = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public class MotionGuard : ControlHandlerTests
	{
		public static TheoryData<string> MotionCommands => ["home", "jog", "extrude", "motors-off"];

		private ValueTask<Result> Run(string command) => command switch
		{
			"home" => new HomeHandler(_session).Handle(new HomeCommand(Axis.All), Ct),
			"jog" => new JogHandler(_session, _settings).Handle(new JogCommand(Axis.X, 10), Ct),
			"extrude" => new ExtrudeHandler(_session, _settings).Handle(new ExtrudeCommand(5), Ct),
			_ => new MotorsOffHandler(_session).Handle(new MotorsOffCommand(), Ct)
		};

		[Theory]
		[MemberData(nameof(MotionCommands))]
		public async Task Is_refused_while_printing(string command)
		{
			_session.State = PrinterState.Printing;
			Result r = await Run(command);
			Assert.Equal("Pause the print before moving the printer.", r.Error);
			Assert.Empty(_session.Enqueued);
		}

		[Theory]
		[MemberData(nameof(MotionCommands))]
		public async Task Is_refused_when_not_connected(string command)
		{
			_session.State = PrinterState.Offline;
			Result r = await Run(command);
			Assert.Equal("Printer is not connected.", r.Error);
			Assert.Empty(_session.Enqueued);
		}

		[Fact]
		public async Task Is_allowed_while_paused()
		{
			_session.State = PrinterState.Paused;
			Assert.True((await Run("home")).IsSuccess);
		}
	}

	public class Home : ControlHandlerTests
	{
		[Theory]
		[InlineData(Axis.All, "G28")]
		[InlineData(Axis.None, "G28")]
		[InlineData(Axis.Z, "G28 Z")]
		[InlineData(Axis.X | Axis.Y, "G28 X Y")]
		public async Task Homes_the_requested_axes(Axis axes, string expected)
		{
			Assert.True((await new HomeHandler(_session).Handle(new HomeCommand(axes), Ct)).IsSuccess);
			Assert.Equal([expected], _session.Enqueued);
		}
	}

	public class Jog : ControlHandlerTests
	{
		private ValueTask<Result> Run(Axis axis, double distance) =>
			new JogHandler(_session, _settings).Handle(new JogCommand(axis, distance), Ct);

		[Fact]
		public async Task Moves_relative_with_the_xy_feedrate()
		{
			await Run(Axis.X, 10);
			Assert.Equal(["G91", "G1 X10 F6000", "G90"], _session.Enqueued);
		}

		[Fact]
		public async Task Uses_the_z_feedrate_for_z()
		{
			await Run(Axis.Z, -0.1);
			Assert.Equal("G1 Z-0.1 F600", _session.Enqueued[1]);
		}

		[Fact]
		public async Task Formats_distance_with_three_decimals()
		{
			await Run(Axis.Y, 1.23456);
			Assert.Equal("G1 Y1.235 F6000", _session.Enqueued[1]);
		}

		[Theory]
		[InlineData(Axis.X | Axis.Y, 10)]
		[InlineData(Axis.None, 10)]
		[InlineData(Axis.X, 0)]
		[InlineData(Axis.X, 500.1)]
		[InlineData(Axis.X, -501)]
		public async Task Rejects_invalid_moves(Axis axis, double distance)
		{
			Assert.False((await Run(axis, distance)).IsSuccess);
			Assert.Empty(_session.Enqueued);
		}
	}

	public class Extrude : ControlHandlerTests
	{
		private ValueTask<Result> Run(double mm, int tool = 0) =>
			new ExtrudeHandler(_session, _settings).Handle(new ExtrudeCommand(mm, tool), Ct);

		[Fact]
		public async Task Extrudes_relative_on_a_hot_nozzle()
		{
			_session.ReportTemperatures(new HeaterReading("T0", 200, 200));
			Assert.True((await Run(5)).IsSuccess);
			Assert.Equal(["T0", "M83", "G1 E5 F300", "M82"], _session.Enqueued);
		}

		[Fact]
		public async Task Retracts_with_a_negative_length()
		{
			_session.ReportTemperatures(new HeaterReading("T0", 200, 200));
			await Run(-2);
			Assert.Equal("G1 E-2 F300", _session.Enqueued[2]);
		}

		[Fact]
		public async Task Checks_the_selected_tool()
		{
			_session.ReportTemperatures(new HeaterReading("T0", 200, 200), new HeaterReading("T1", 25, 0));
			Assert.False((await Run(5, tool: 1)).IsSuccess);
		}

		[Theory]
		[InlineData(169.9)]
		[InlineData(25)]
		public async Task Refuses_a_cold_nozzle(double actual)
		{
			_session.ReportTemperatures(new HeaterReading("T0", actual, 200));
			Result r = await Run(5);
			Assert.Equal("Hotend below 170°C — heat it first.", r.Error);
			Assert.Empty(_session.Enqueued);
		}

		[Fact]
		public async Task Refuses_without_a_temperature_report()
		{
			Assert.False((await Run(5)).IsSuccess);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(200.5)]
		[InlineData(-201)]
		public async Task Rejects_invalid_lengths(double mm)
		{
			_session.ReportTemperatures(new HeaterReading("T0", 200, 200));
			Assert.False((await Run(mm)).IsSuccess);
			Assert.Empty(_session.Enqueued);
		}
	}

	public class MotorsOff : ControlHandlerTests
	{
		[Fact]
		public async Task Disables_steppers()
		{
			Assert.True((await new MotorsOffHandler(_session).Handle(new MotorsOffCommand(), Ct)).IsSuccess);
			Assert.Equal(["M84"], _session.Enqueued);
		}
	}

	public class Fan : ControlHandlerTests
	{
		[Theory]
		[InlineData(0, "M107")]
		[InlineData(50, "M106 S127")]
		[InlineData(100, "M106 S255")]
		public async Task Sets_fan_speed(int percent, string expected)
		{
			Assert.True((await new SetFanHandler(_session).Handle(new SetFanCommand(percent), Ct)).IsSuccess);
			Assert.Equal([expected], _session.Enqueued);
		}

		[Theory]
		[InlineData(-1)]
		[InlineData(101)]
		public async Task Rejects_out_of_range(int percent)
		{
			Assert.False((await new SetFanHandler(_session).Handle(new SetFanCommand(percent), Ct)).IsSuccess);
			Assert.Empty(_session.Enqueued);
		}
	}

	public class Rates : ControlHandlerTests
	{
		[Theory]
		[InlineData(50)]
		[InlineData(120)]
		[InlineData(200)]
		public async Task Sets_the_feed_rate_even_while_printing(int percent)
		{
			_session.State = PrinterState.Printing;

			Assert.True((await new SetFeedRateHandler(_session).Handle(new SetFeedRateCommand(percent), Ct)).IsSuccess);
			Assert.Equal([$"M220 S{percent}"], _session.Enqueued);
		}

		[Theory]
		[InlineData(75)]
		[InlineData(97)]
		[InlineData(125)]
		public async Task Sets_the_flow(int percent)
		{
			Assert.True((await new SetFlowRateHandler(_session).Handle(new SetFlowRateCommand(percent), Ct)).IsSuccess);
			Assert.Equal([$"M221 S{percent}"], _session.Enqueued);
		}

		[Theory]
		[InlineData("feed", 49, "Feed rate is 50–200 %.")]
		[InlineData("feed", 201, "Feed rate is 50–200 %.")]
		[InlineData("flow", 74, "Flow is 75–125 %.")]
		[InlineData("flow", 126, "Flow is 75–125 %.")]
		public async Task Rejects_out_of_range(string which, int percent, string error)
		{
			Result r = which == "feed"
				? await new SetFeedRateHandler(_session).Handle(new SetFeedRateCommand(percent), Ct)
				: await new SetFlowRateHandler(_session).Handle(new SetFlowRateCommand(percent), Ct);

			Assert.Equal(error, r.Error);
			Assert.Empty(_session.Enqueued);
		}
	}

	public class SendGcode : ControlHandlerTests
	{
		[Fact]
		public async Task Sends_each_non_empty_line_trimmed()
		{
			Assert.True((await new SendGcodeHandler(_session).Handle(new SendGcodeCommand("G28\n\n  M105 \r\n"), Ct)).IsSuccess);
			Assert.Equal(["G28", "M105"], _session.Enqueued);
		}

		[Theory]
		[InlineData("")]
		[InlineData(" \n \r\n")]
		public async Task Refuses_empty_input(string text)
		{
			Result r = await new SendGcodeHandler(_session).Handle(new SendGcodeCommand(text), Ct);
			Assert.Equal("Nothing to send.", r.Error);
		}
	}
}
