using System.Globalization;

using PrintDeck.Domain.Settings.Enums;

namespace PrintDeck.Web.Services;

/// <summary>
/// The CSS that turns and mirrors the camera picture in the browser. The image is centred in its 16:9 box;
/// turned a quarter, it is sized to the box turned on its side so that it still fits after rotating.
/// </summary>
public static class CameraView
{
	public static bool IsSideways(CameraRotation rotation) => rotation is CameraRotation.Clockwise90 or CameraRotation.Clockwise270;

	/// <summary>Inline style for the <c>img</c>: flips first, then the rotation (CSS applies transforms right to left).</summary>
	public static string ImageStyle(CameraRotation rotation, bool flipHorizontal, bool flipVertical)
	{
		string transform = "translate(-50%, -50%)";
		if (rotation != CameraRotation.None) transform += $" rotate({((int)rotation).ToString(CultureInfo.InvariantCulture)}deg)";
		if (flipHorizontal || flipVertical) transform += $" scale({(flipHorizontal ? -1 : 1)}, {(flipVertical ? -1 : 1)})";

		// 16:9 box: on its side the image needs the box's height as width (9/16 = 56.25 %) and its width as height (16/9).
		string size = IsSideways(rotation) ? "width: 56.25%; height: 177.78%;" : "width: 100%; height: 100%;";
		return $"{size} transform: {transform};";
	}
}
