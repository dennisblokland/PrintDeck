using Mediator;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Settings.Enums;

namespace PrintDeck.Application.Features.Settings.Commands;

/// <summary>Host-wide settings that don't depend on the printer profile.</summary>
public sealed record UpdateSettingsCommand(
	IReadOnlyList<TemperaturePreset> Presets,
	string? WebcamStreamUrl,
	bool ShowProgressOnPrinter = true,
	bool AutoConnect = false,
	string? WebcamSnapshotUrl = null,
	CameraRotation WebcamRotation = CameraRotation.None,
	bool WebcamFlipHorizontal = false,
	bool WebcamFlipVertical = false)
	: ICommand<Result>;
