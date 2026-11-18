using PrintDeck.Domain.Settings.Enums;
using PrintDeck.Web.Services;

namespace PrintDeck.Web.Tests.Services;

public class CameraViewTests
{
	[Theory]
	[InlineData(CameraRotation.None, false, false, "width: 100%; height: 100%; transform: translate(-50%, -50%);")]
	[InlineData(CameraRotation.Half, false, false, "width: 100%; height: 100%; transform: translate(-50%, -50%) rotate(180deg);")]
	[InlineData(CameraRotation.Clockwise90, false, false, "width: 56.25%; height: 177.78%; transform: translate(-50%, -50%) rotate(90deg);")]
	[InlineData(CameraRotation.Clockwise270, true, false, "width: 56.25%; height: 177.78%; transform: translate(-50%, -50%) rotate(270deg) scale(-1, 1);")]
	[InlineData(CameraRotation.None, false, true, "width: 100%; height: 100%; transform: translate(-50%, -50%) scale(1, -1);")]
	[InlineData(CameraRotation.None, true, true, "width: 100%; height: 100%; transform: translate(-50%, -50%) scale(-1, -1);")]
	public void Image_style_turns_and_mirrors(CameraRotation rotation, bool flipH, bool flipV, string expected) =>
		Assert.Equal(expected, CameraView.ImageStyle(rotation, flipH, flipV));
}
