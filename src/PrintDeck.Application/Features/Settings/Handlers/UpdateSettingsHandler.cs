using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Settings.Enums;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class UpdateSettingsHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<UpdateSettingsCommand, Result>
{
	public async ValueTask<Result> Handle(UpdateSettingsCommand c, CancellationToken ct)
	{
		string? webcam = string.IsNullOrWhiteSpace(c.WebcamStreamUrl) ? null : c.WebcamStreamUrl.Trim();
		string? snapshot = string.IsNullOrWhiteSpace(c.WebcamSnapshotUrl) ? null : c.WebcamSnapshotUrl.Trim();
		List<TemperaturePreset> presets = c.Presets.Select(p => p with { Name = p.Name?.Trim() ?? "" }).ToList();

		string? error =
			presets.Any(x => x.Name.Length == 0 || x.Hotend is < 0 or > 320 || x.Bed is < 0 or > 130)
				? "Presets need a name, hotend 0–320 °C and bed 0–130 °C."
			: presets.DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Count() != presets.Count
				? "Preset names must be unique."
			: webcam is not null && !Uri.TryCreate(webcam, UriKind.Absolute, out _)
				? "Webcam URL is not a valid URL."
			: snapshot is not null && !IsHttpUrl(snapshot)
				? "Snapshot URL must be an http or https URL."
			: !Enum.IsDefined(c.WebcamRotation)
				? "Rotation must be 0, 90, 180 or 270 degrees."
			: null;
		if (error is not null) return Result.Failure(error);

		await settings.UpdateAsync(s => s with
		{
			Presets = presets,
			WebcamStreamUrl = webcam,
			WebcamSnapshotUrl = snapshot,
			WebcamRotation = c.WebcamRotation,
			WebcamFlipHorizontal = c.WebcamFlipHorizontal,
			WebcamFlipVertical = c.WebcamFlipVertical,
			ShowProgressOnPrinter = c.ShowProgressOnPrinter,
			AutoConnect = c.AutoConnect
		}, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}

	/// <summary>PrintDeck fetches snapshots itself, so only web addresses (no file: or other schemes).</summary>
	public static bool IsHttpUrl(string url) =>
		Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
