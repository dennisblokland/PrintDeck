using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Logs.Commands;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Logs.Handlers;

public sealed class SetSerialLogHandler(ISettingsStore settings, ISerialLog serialLog, PrinterEventHub events)
	: ICommandHandler<SetSerialLogCommand, Result>
{
	public async ValueTask<Result> Handle(SetSerialLogCommand c, CancellationToken ct)
	{
		await settings.UpdateAsync(s => s with { SerialLogEnabled = c.Enabled }, ct);
		serialLog.SetEnabled(c.Enabled);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
