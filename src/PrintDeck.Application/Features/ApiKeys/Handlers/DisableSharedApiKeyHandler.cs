using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.ApiKeys.Handlers;

public sealed class DisableSharedApiKeyHandler(ISettingsStore settings, PrinterEventHub events) : ICommandHandler<DisableSharedApiKeyCommand, Result>
{
	public async ValueTask<Result> Handle(DisableSharedApiKeyCommand c, CancellationToken ct)
	{
		// A fresh key too, so a backup restored later or a stray "on" can't bring the old one back.
		await settings.UpdateAsync(s => s with { SharedApiKeyEnabled = false, ApiKey = RegenerateApiKeyHandler.NewKey() }, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
