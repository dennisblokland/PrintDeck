using System.Security.Cryptography;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class UpdatePrinterProfileHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<UpdatePrinterProfileCommand, Result>
{
	public async ValueTask<Result> Handle(UpdatePrinterProfileCommand c, CancellationToken ct)
	{
		PrinterProfile p = c.Profile;
		string? error = p switch
		{
			{ Name.Length: 0 } => "Name is required.",
			{ BedWidth: <= 0 or > 2000 } or { BedDepth: <= 0 or > 2000 } or { MaxHeight: <= 0 or > 2000 } => "Build volume must be 1–2000 mm.",
			{ ExtruderCount: < 1 or > 8 } => "Extruder count must be 1–8.",
			{ JogFeedrateXY: <= 0 } or { JogFeedrateZ: <= 0 } or { ExtrudeFeedrate: <= 0 } => "Feedrates must be positive.",
			_ when p.Presets.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Hotend is < 0 or > 320 || x.Bed is < 0 or > 130)
				=> "Presets need a name, hotend 0–320 °C and bed 0–130 °C.",
			_ when p.WebcamStreamUrl is { Length: > 0 } url && !Uri.TryCreate(url, UriKind.Absolute, out _) => "Webcam URL is not a valid URL.",
			_ => null
		};
		if (error is not null) return Result.Failure(error);

		PrinterProfile current = await settings.GetAsync(ct);
		await settings.SaveAsync(p with { ApiKey = current.ApiKey }, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
