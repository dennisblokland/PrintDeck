using System.Globalization;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Features.Control.Handlers;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;

namespace PrintDeck.Application.Features.Control;

internal static class Guard
{
	/// <summary>Manual motion is allowed when idle or paused, never mid-print.</summary>
	public static Result CanMove(IPrinterSession s) => s.State switch
	{
		PrinterState.Operational or PrinterState.Paused => Result.Success(),
		PrinterState.Printing => Result.Failure("Pause the print before moving the printer."),
		_ => Result.Failure("Printer is not connected.")
	};

	public static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
