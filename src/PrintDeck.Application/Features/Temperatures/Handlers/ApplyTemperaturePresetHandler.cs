using System.Globalization;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Temperatures.Commands;
using PrintDeck.Application.Features.Temperatures.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Features.Temperatures.Handlers;

public sealed class ApplyTemperaturePresetHandler(IPrinterSession session, ISettingsStore settings)
	: ICommandHandler<ApplyTemperaturePresetCommand, Result>
{
	public async ValueTask<Result> Handle(ApplyTemperaturePresetCommand c, CancellationToken ct)
	{
		PrinterProfile profile = await settings.GetAsync(ct);
		if (c.Preset == "Off")
			return session.Enqueue(Enumerable.Range(0, profile.ExtruderCount).Select(t => $"M104 T{t} S0").Append("M140 S0"));

		TemperaturePreset? preset = profile.Presets.FirstOrDefault(p => p.Name == c.Preset);
		if (preset is null) return Result.Failure($"Unknown preset {c.Preset}.");

		List<string> commands = Enumerable.Range(0, profile.ExtruderCount).Select(t => $"M104 T{t} S{preset.Hotend}").ToList();
		if (profile.HasHeatedBed) commands.Add($"M140 S{preset.Bed}");
		return session.Enqueue(commands);
	}
}
