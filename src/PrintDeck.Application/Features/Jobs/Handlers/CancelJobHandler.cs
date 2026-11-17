using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Jobs.Handlers;

public sealed class CancelJobHandler(IPrinterSession session, ISettingsStore settings) : ICommandHandler<CancelJobCommand, Result>
{
	public async ValueTask<Result> Handle(CancelJobCommand c, CancellationToken ct)
	{
		HostSettings s = await settings.GetAsync(ct);
		return session.CancelJob(PrintScripts.From(s).Cancel);
	}
}
