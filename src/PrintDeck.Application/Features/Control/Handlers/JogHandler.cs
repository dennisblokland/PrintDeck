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

namespace PrintDeck.Application.Features.Control.Handlers;

public sealed class JogHandler(IPrinterSession session, ISettingsStore settings) : ICommandHandler<JogCommand, Result>
{
	public async ValueTask<Result> Handle(JogCommand c, CancellationToken ct)
	{
		if (Guard.CanMove(session) is { IsSuccess: false } blocked) return blocked;
		if (c.Axis is not (Axis.X or Axis.Y or Axis.Z)) return Result.Failure("Jog one axis at a time.");
		if (c.Distance == 0 || Math.Abs(c.Distance) > 500) return Result.Failure("Distance must be between 0 and 500 mm.");

		PrinterProfile p = (await settings.GetAsync(ct)).ActiveProfile;
		int feed = c.Axis == Axis.Z ? p.JogFeedrateZ : p.JogFeedrateXY;
		return session.Enqueue(["G91", $"G1 {c.Axis}{Guard.F(c.Distance)} F{feed}", "G90"]);
	}
}
