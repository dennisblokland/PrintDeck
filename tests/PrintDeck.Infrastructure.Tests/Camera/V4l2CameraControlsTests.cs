using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Infrastructure.Camera;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Camera;

public class V4l2CameraControlsTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private sealed class ScriptedRunner(Func<IReadOnlyList<string>, CommandOutput?> answer) : ICommandRunner
	{
		public List<(string Program, IReadOnlyList<string> Arguments)> Runs { get; } = [];

		public Task<CommandOutput?> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
		{
			Runs.Add((program, arguments));
			return Task.FromResult(answer(arguments));
		}
	}

	private sealed class SlowRunner : ICommandRunner
	{
		public Task<CommandOutput?> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct) =>
			throw new TimeoutException();
	}

	private static V4l2CameraControls Controls(ICommandRunner host) => new(host, new VirtualV4l2Ctl(), () => ["/dev/video0"]);

	[Fact]
	public void Lists_host_devices_and_the_virtual_camera()
	{
		Assert.Equal(["/dev/video0", "VIRTUAL"], Controls(new SlowRunner()).Devices());
	}

	[Fact]
	public async Task Runs_v4l2_ctl_for_a_host_device()
	{
		ScriptedRunner host = new(_ => new CommandOutput(0, "brightness 0x00980900 (int)    : min=0 max=255 step=1 default=128 value=128\n", ""));

		Result<IReadOnlyList<CameraControl>> r = await Controls(host).ListAsync("/dev/video0", Ct);

		Assert.Equal("brightness", Assert.Single(r.Value!).Name);
		Assert.Equal("v4l2-ctl", host.Runs[0].Program);
		Assert.Equal(["-d", "/dev/video0", "--list-ctrls-menus"], host.Runs[0].Arguments);
	}

	[Fact]
	public async Task Sets_each_value_with_its_own_call()
	{
		ScriptedRunner host = new(_ => new CommandOutput(0, "", ""));

		Assert.True((await Controls(host).SetAsync("/dev/video0", [new("auto_exposure", 1), new("exposure_time_absolute", 400)], Ct)).IsSuccess);

		Assert.Equal(["--set-ctrl=auto_exposure=1", "--set-ctrl=exposure_time_absolute=400"], host.Runs.Select(r => r.Arguments[2]));
	}

	[Fact]
	public async Task Only_talks_to_listed_devices_and_control_names()
	{
		ScriptedRunner host = new(_ => new CommandOutput(0, "", ""));
		V4l2CameraControls controls = Controls(host);

		Assert.Equal("That camera is not connected.", (await controls.ListAsync("/dev/sda", Ct)).Error);
		Assert.Equal("'brightness=1 --all' is not a camera control.", (await controls.SetAsync("/dev/video0", [new("brightness=1 --all", 1)], Ct)).Error);
		Assert.Empty(host.Runs);
	}

	[Fact]
	public async Task Explains_failures()
	{
		Assert.Equal("v4l2-ctl is not installed. Install v4l-utils on the host.",
			(await Controls(new ScriptedRunner(_ => null)).ListAsync("/dev/video0", Ct)).Error);
		Assert.Equal("The camera did not answer.", (await Controls(new SlowRunner()).ListAsync("/dev/video0", Ct)).Error);
		Assert.Equal("The camera refused: Cannot open device /dev/video0, exiting.",
			(await Controls(new ScriptedRunner(_ => new CommandOutput(1, "", "Cannot open device /dev/video0, exiting.\n"))).ListAsync("/dev/video0", Ct)).Error);
		Assert.Equal("v4l2-ctl failed (exit code 3).",
			(await Controls(new ScriptedRunner(_ => new CommandOutput(3, "", ""))).ListAsync("/dev/video0", Ct)).Error);
	}

	[Fact]
	public async Task Virtual_camera_lists_parseable_controls()
	{
		Result<IReadOnlyList<CameraControl>> r = await Controls(new SlowRunner()).ListAsync(VirtualV4l2Ctl.DeviceName, Ct);

		Assert.True(r.IsSuccess, r.Error);
		Assert.Contains(r.Value!, c => c is { Name: "brightness", Min: -64, Max: 64, Value: 0 });
		CameraControl exposure = r.Value!.Single(c => c.Name == "auto_exposure");
		Assert.Equal(["Manual Mode", "Aperture Priority Mode"], exposure.Menu.Select(m => m.Label));
		Assert.True(r.Value!.Single(c => c.Name == "exposure_time_absolute").Inactive);
		Assert.Equal(5, r.Value!.Single(c => c.Name == "focus_absolute").Step);
	}

	[Fact]
	public async Task Virtual_camera_keeps_values_and_unlocks_manual_controls()
	{
		V4l2CameraControls controls = Controls(new SlowRunner());

		Result set = await controls.SetAsync(VirtualV4l2Ctl.DeviceName, [new("auto_exposure", 1), new("exposure_time_absolute", 400), new("brightness", -10)], Ct);

		Assert.True(set.IsSuccess, set.Error);
		IReadOnlyList<CameraControl> after = (await controls.ListAsync(VirtualV4l2Ctl.DeviceName, Ct)).Value!;
		Assert.Equal(1, after.Single(c => c.Name == "auto_exposure").Value);
		CameraControl exposureTime = after.Single(c => c.Name == "exposure_time_absolute");
		Assert.Equal((400L, false), (exposureTime.Value, exposureTime.Inactive));
		Assert.Equal(-10, after.Single(c => c.Name == "brightness").Value);
	}

	[Theory]
	[InlineData("brightness", 65, "The camera refused: Error setting controls: Numerical result out of range")]
	[InlineData("focus_absolute", 7, "The camera refused: Error setting controls: Numerical result out of range")]
	[InlineData("auto_exposure", 2, "The camera refused: Error setting controls: Numerical result out of range")]
	[InlineData("zoom_absolute", 100, "The camera refused: unknown control 'zoom_absolute'")]
	public async Task Virtual_camera_refuses_like_v4l2_ctl(string name, long value, string error)
	{
		Assert.Equal(error, (await Controls(new SlowRunner()).SetAsync(VirtualV4l2Ctl.DeviceName, [new(name, value)], Ct)).Error);
	}
}
