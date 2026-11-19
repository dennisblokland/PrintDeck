using PrintDeck.Domain.Settings.Enums;

namespace PrintDeck.Domain.Timelapse;

/// <summary>How the frames of a timelapse become a video: its frame rate, and the camera's rotate and flip settings.</summary>
public sealed record TimelapseRenderOptions(int FrameRate, CameraRotation Rotation = CameraRotation.None, bool FlipHorizontal = false, bool FlipVertical = false);
