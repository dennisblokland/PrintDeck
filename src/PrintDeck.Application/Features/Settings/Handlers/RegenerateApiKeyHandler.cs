using System.Security.Cryptography;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class RegenerateApiKeyHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<RegenerateApiKeyCommand, Result<string>>
{
	public async ValueTask<Result<string>> Handle(RegenerateApiKeyCommand c, CancellationToken ct)
	{
		string key = NewKey();
		PrinterProfile p = await settings.GetAsync(ct);
		await settings.SaveAsync(p with { ApiKey = key }, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success(key);
	}

	public static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
