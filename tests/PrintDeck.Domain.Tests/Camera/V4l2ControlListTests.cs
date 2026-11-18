using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Camera.Enums;

namespace PrintDeck.Domain.Tests.Camera;

public class V4l2ControlListTests
{
	// v4l2-ctl 1.22 for a Logitech C920, trimmed, plus a few control types PrintDeck skips.
	private const string C920 = """

		User Controls

		                     brightness 0x00980900 (int)    : min=0 max=255 step=1 default=128 value=140
		                       contrast 0x00980901 (int)    : min=0 max=255 step=1 default=128 value=128
		        white_balance_automatic 0x0098090c (bool)   : default=1 value=1
		           power_line_frequency 0x00980918 (menu)   : min=0 max=2 default=2 value=1 (50 Hz)
						0: Disabled
						1: 50 Hz
						2: 60 Hz
		      white_balance_temperature 0x0098091a (int)    : min=2000 max=6500 step=1 default=4000 value=4000 flags=inactive
		                      sharpness 0x0098091b (int)    : min=0 max=255 step=1 default=128 value=128

		Camera Controls

		                  auto_exposure 0x009a0901 (menu)   : min=0 max=3 default=3 value=3 (Aperture Priority Mode)
						1: Manual Mode
						3: Aperture Priority Mode
		         exposure_time_absolute 0x009a0902 (int)    : min=3 max=2047 step=1 default=250 value=333 flags=inactive
		                 focus_absolute 0x009a090a (int)    : min=0 max=250 step=5 default=0 value=0 flags=inactive
		     focus_automatic_continuous 0x009a090c (bool)   : default=1 value=1
		                  zoom_absolute 0x009a090d (int)    : min=100 max=500 step=1 default=100 value=100
		                    pan_reset 0x009a0906 (button) : flags=write-only, execute-on-write
		                   led1_mode 0x0a046d05 (menu)   : min=0 max=3 default=0 value=0 flags=disabled
						0: Off
						1: On
		                camera_name 0x00a00001 (str)    : min=0 max=32 step=1 value='C920' flags=read-only
		                    bitrate 0x009909cf (intmenu): min=0 max=1 default=0 value=1 (2000000 0x1e8480)
						0: 1000000 (0x000f4240)
						1: 2000000 (0x001e8480)
		""";

	private static readonly IReadOnlyList<CameraControl> _controls = V4l2ControlList.Parse(C920);

	private static CameraControl Control(string name) => _controls.Single(c => c.Name == name);

	[Fact]
	public void Reads_the_supported_controls_in_order()
	{
		Assert.Equal(
			["brightness", "contrast", "white_balance_automatic", "power_line_frequency", "white_balance_temperature", "sharpness",
			 "auto_exposure", "exposure_time_absolute", "focus_absolute", "focus_automatic_continuous", "zoom_absolute", "bitrate"],
			_controls.Select(c => c.Name));
	}

	[Fact]
	public void Reads_an_integer_control()
	{
		Assert.Equal(new CameraControl("brightness", CameraControlType.Integer, 0, 255, 1, 128, 140, false, []), Control("brightness") with { Menu = [] });
		Assert.Equal(5, Control("focus_absolute").Step);
	}

	[Fact]
	public void Reads_a_boolean_control_as_zero_to_one()
	{
		CameraControl c = Control("focus_automatic_continuous");
		Assert.Equal((CameraControlType.Boolean, 0L, 1L, 1L, 1L), (c.Type, c.Min, c.Max, c.Default, c.Value));
	}

	[Fact]
	public void Reads_menus_with_their_choices()
	{
		CameraControl exposure = Control("auto_exposure");
		Assert.Equal(CameraControlType.Menu, exposure.Type);
		Assert.Equal(3, exposure.Value);
		Assert.Equal([new CameraMenuItem(1, "Manual Mode"), new CameraMenuItem(3, "Aperture Priority Mode")], exposure.Menu);
		Assert.Equal([0L, 1L, 2L], Control("power_line_frequency").Menu.Select(m => m.Value));
		Assert.Equal("2000000 (0x001e8480)", Control("bitrate").Menu[1].Label);
	}

	[Fact]
	public void Marks_inactive_controls()
	{
		Assert.True(Control("exposure_time_absolute").Inactive);
		Assert.True(Control("white_balance_temperature").Inactive);
		Assert.False(Control("brightness").Inactive);
	}

	[Fact]
	public void Reads_nothing_from_empty_or_unrelated_output()
	{
		Assert.Empty(V4l2ControlList.Parse(""));
		Assert.Empty(V4l2ControlList.Parse("Cannot open device /dev/video0, exiting.\n"));
	}

	[Fact]
	public void Accepts_windows_line_endings()
	{
		Assert.Equal(_controls, V4l2ControlList.Parse(C920.Replace("\n", "\r\n")), new ControlComparer());
	}

	private sealed class ControlComparer : IEqualityComparer<CameraControl>
	{
		public bool Equals(CameraControl? x, CameraControl? y) =>
			x is not null && y is not null && x with { Menu = [] } == y with { Menu = [] } && x.Menu.SequenceEqual(y.Menu);

		public int GetHashCode(CameraControl obj) => obj.Name.GetHashCode();
	}

	[Theory]
	[InlineData("brightness", true)]
	[InlineData("focus_absolute", true)]
	[InlineData("brightness=1 --list-devices", false)]
	[InlineData("--all", false)]
	[InlineData("", false)]
	public void Validates_control_names(string name, bool valid) => Assert.Equal(valid, V4l2ControlList.IsValidName(name));
}
