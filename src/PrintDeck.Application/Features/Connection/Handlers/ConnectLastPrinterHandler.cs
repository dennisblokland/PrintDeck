using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Connection.Handlers;

public sealed class ConnectLastPrinterHandler(IPrinterSession session, ISettingsStore settings)
	: ICommandHandler<ConnectLastPrinterCommand, Result>
{
	/// <summary>Longest a connect that is already under way can take: the bootloader wait plus three handshake attempts.</summary>
	public static readonly TimeSpan ConnectingTimeout = TimeSpan.FromSeconds(30);

	public async ValueTask<Result> Handle(ConnectLastPrinterCommand command, CancellationToken ct)
	{
		// Auto-connect or someone in the UI may be connecting right now.
		DateTime deadline = DateTime.UtcNow + ConnectingTimeout;
		while (session.State == PrinterState.Connecting && DateTime.UtcNow < deadline)
			await Task.Delay(100, ct);

		if (session.State.IsConnected()) return Result.Success();
		if (session.State == PrinterState.Connecting) return Result.Failure("Printer is still connecting.");

		HostSettings s = await settings.GetAsync(ct);
		if (string.IsNullOrWhiteSpace(s.LastPort))
			return Result.Failure("Printer is not connected, and there is no last port to connect to. Connect once from PrintDeck first.");

		return await session.ConnectAsync(s.LastPort, s.DefaultBaudRate, ct);
	}
}
