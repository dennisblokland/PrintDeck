using Mediator;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Commands;

/// <summary>Host-wide settings that don't depend on the printer profile.</summary>
public sealed record UpdateSettingsCommand(
	IReadOnlyList<TemperaturePreset> Presets,
	string? WebcamStreamUrl,
	bool ShowProgressOnPrinter = true,
	bool AutoConnect = false)
	: ICommand<Result>;
