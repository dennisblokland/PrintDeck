using System.Globalization;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Features.Control.Handlers;

public sealed class ExtrudeHandler(IPrinterSession session, ISettingsStore settings) : ICommandHandler<ExtrudeCommand, Result>
{
	public const double MinExtrudeTemp = 170;

	public async ValueTask<Result> Handle(ExtrudeCommand c, CancellationToken ct)
	{
		if (Guard.CanMove(session) is { IsSuccess: false } blocked) return blocked;
		if (c.Millimeters == 0 || Math.Abs(c.Millimeters) > 200) return Result.Failure("Length must be between 0 and 200 mm.");

		HeaterReading? hotend = session.Temperatures.Latest?.Heaters.FirstOrDefault(h => h.Heater == $"T{c.Tool}");
		if (hotend is null || hotend.Actual < MinExtrudeTemp)
			return Result.Failure($"Hotend below {MinExtrudeTemp:0}°C — heat it first.");

		PrinterProfile p = (await settings.GetAsync(ct)).ActiveProfile;
		return session.Enqueue([$"T{c.Tool}", "M83", $"G1 E{Guard.F(c.Millimeters)} F{p.ExtrudeFeedrate}", "M82"]);
	}
}
