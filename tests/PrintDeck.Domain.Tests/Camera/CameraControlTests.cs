using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Camera.Enums;

namespace PrintDeck.Domain.Tests.Camera;

public class CameraControlTests
{
	private static readonly CameraControl _focus = new("focus_absolute", CameraControlType.Integer, 0, 250, 5, 0, 0, false, []);
	private static readonly CameraControl _autofocus = new("focus_automatic_continuous", CameraControlType.Boolean, 0, 1, 1, 1, 1, false, []);
	private static readonly CameraControl _exposure = new("auto_exposure", CameraControlType.Menu, 0, 3, 1, 3, 3, false,
		[new CameraMenuItem(1, "Manual Mode"), new CameraMenuItem(3, "Aperture Priority Mode")]);

	[Fact]
	public void Label_is_the_name_in_words() => Assert.Equal("Focus automatic continuous", _autofocus.Label);

	[Theory]
	[InlineData(0, null)]
	[InlineData(250, null)]
	[InlineData(125, null)]
	[InlineData(-5, "Focus absolute must be between 0 and 250.")]
	[InlineData(255, "Focus absolute must be between 0 and 250.")]
	[InlineData(7, "Focus absolute goes in steps of 5 from 0.")]
	public void Checks_an_integer_against_range_and_step(long value, string? error) => Assert.Equal(error, _focus.Check(value));

	[Fact]
	public void Checks_booleans_and_menus()
	{
		Assert.Null(_autofocus.Check(0));
		Assert.Equal("Focus automatic continuous is on or off.", _autofocus.Check(2));
		Assert.Null(_exposure.Check(1));
		Assert.Equal("Auto exposure has no option 2.", _exposure.Check(2));
	}

	[Fact]
	public void Applies_switches_and_menus_before_the_values_they_unlock_and_drops_unknown_ones()
	{
		IReadOnlyList<CameraControlValue> ordered = CameraControl.InApplyOrder(
			[new("focus_absolute", 50), new("gone", 1), new("auto_exposure", 1), new("focus_automatic_continuous", 0)],
			[_focus, _autofocus, _exposure]);

		Assert.Equal(["auto_exposure", "focus_automatic_continuous", "focus_absolute"], ordered.Select(v => v.Name));
	}
}
