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
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Features.Temperatures.Handlers;

public sealed class SetTemperatureHandler(IPrinterSession session) : ICommandHandler<SetTemperatureCommand, Result>
{
	public ValueTask<Result> Handle(SetTemperatureCommand c, CancellationToken ct) =>
		ValueTask.FromResult(Build(c) is { } cmd ? session.Enqueue([cmd]) : Result.Failure(Invalid(c)));

	internal static string? Build(SetTemperatureCommand c)
	{
		string s = c.Target.ToString("0", CultureInfo.InvariantCulture);
		return c.Heater switch
		{
			"B" when c.Target is >= 0 and <= 130 => $"M140 S{s}",
			"C" when c.Target is >= 0 and <= 80 => $"M141 S{s}",
			['T', .. string n] when int.TryParse(n, out int tool) && c.Target is >= 0 and <= 320 => $"M104 T{tool} S{s}",
			_ => null
		};
	}

	private static string Invalid(SetTemperatureCommand c) => $"Invalid target {c.Target}°C for {c.Heater}.";
}
