using Mediator;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Settings.Enums;
using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Application.Features.Settings.Commands;

/// <summary>Host-wide settings that don't depend on the printer profile.</summary>
/// <param name="Timelapse">Timelapse settings; null keeps the saved ones.</param>
public sealed record UpdateSettingsCommand(
	IReadOnlyList<TemperaturePreset> Presets,
	string? WebcamStreamUrl,
	bool ShowProgressOnPrinter = true,
	bool AutoConnect = false,
	string? WebcamSnapshotUrl = null,
	CameraRotation WebcamRotation = CameraRotation.None,
	bool WebcamFlipHorizontal = false,
	bool WebcamFlipVertical = false,
	TimelapseSettings? Timelapse = null)
	: ICommand<Result>;
