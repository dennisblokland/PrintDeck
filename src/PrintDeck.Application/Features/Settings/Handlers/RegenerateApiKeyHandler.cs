using System.Security.Cryptography;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class RegenerateApiKeyHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<RegenerateApiKeyCommand, Result<string>>
{
	public async ValueTask<Result<string>> Handle(RegenerateApiKeyCommand c, CancellationToken ct)
	{
		string key = NewKey();
		await settings.UpdateAsync(s => s with { ApiKey = key, SharedApiKeyEnabled = true }, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success(key);
	}

	public static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
