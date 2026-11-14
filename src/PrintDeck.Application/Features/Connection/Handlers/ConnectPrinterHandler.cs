using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Application.Features.Connection.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Connection.Handlers;

public sealed class ConnectPrinterHandler(IPrinterSession session, ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<ConnectPrinterCommand, Result>
{
	public static readonly int[] SupportedBaudRates = [250000, 230400, 115200, 57600, 38400, 19200, 9600];

	public async ValueTask<Result> Handle(ConnectPrinterCommand command, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(command.Port)) return Result.Failure("Choose a port.");
		if (!SupportedBaudRates.Contains(command.BaudRate)) return Result.Failure($"Unsupported baud rate {command.BaudRate}.");

		Result result = await session.ConnectAsync(command.Port, command.BaudRate, ct);
		if (result.IsSuccess)
		{
			PrinterProfile profile = await settings.GetAsync(ct);
			await settings.SaveAsync(profile with { LastPort = command.Port, DefaultBaudRate = command.BaudRate }, ct);
			events.Publish(PrinterChange.Settings);
		}
		return result;
	}
}
