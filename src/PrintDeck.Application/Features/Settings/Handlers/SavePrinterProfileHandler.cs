using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class SavePrinterProfileHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<SavePrinterProfileCommand, Result<string>>
{
	public const int MaxProfiles = 32;

	public async ValueTask<Result<string>> Handle(SavePrinterProfileCommand c, CancellationToken ct)
	{
		PrinterProfile p = c.Profile with { Name = c.Profile.Name?.Trim() ?? "" };
		if (Validate(p) is { } invalid) return Result.Failure<string>(invalid);

		bool adding = string.IsNullOrEmpty(p.Id);
		if (adding) p = p with { Id = PrinterProfile.NewId() };

		string? error = null;
		await settings.UpdateAsync(s =>
		{
			error = s switch
			{
				_ when !adding && s.FindProfile(p.Id) is null => "That profile no longer exists.",
				_ when adding && s.Profiles.Count >= MaxProfiles => $"You can keep up to {MaxProfiles} profiles.",
				_ when s.Profiles.Any(x => x.Id != p.Id && string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase))
					=> $"A profile named {p.Name} already exists.",
				_ => null
			};
			if (error is not null) return s;
			return s with { Profiles = adding ? [.. s.Profiles, p] : s.Profiles.Select(x => x.Id == p.Id ? p : x).ToList() };
		}, ct);

		if (error is not null) return Result.Failure<string>(error);
		events.Publish(PrinterChange.Settings);
		return Result.Success(p.Id);
	}

	public static string? Validate(PrinterProfile p) => p switch
	{
		{ Name.Length: 0 } => "Name is required.",
		{ Name.Length: > 64 } => "Name can be at most 64 characters.",
		{ BedWidth: <= 0 or > 2000 } or { BedDepth: <= 0 or > 2000 } or { MaxHeight: <= 0 or > 2000 } => "Build volume must be 1–2000 mm.",
		{ ExtruderCount: < 1 or > 8 } => "Extruder count must be 1–8.",
		{ JogFeedrateXY: <= 0 } or { JogFeedrateZ: <= 0 } or { ExtrudeFeedrate: <= 0 } => "Feedrates must be positive.",
		_ => null
	};
}
