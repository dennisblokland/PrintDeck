using System.Text.Json.Nodes;

using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Modules.Queries;
using PrintDeck.Application.Plugins;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Modules.Handlers;

public sealed class GetModuleSecretSettingsHandler(ModuleCatalog catalog, ISettingsStore settings, ICurrentUser currentUser)
	: IQueryHandler<GetModuleSecretSettingsQuery, Result<JsonObject>>
{
	public const string OwnSecretsOnly = "A module can only read its own settings.";

	public async ValueTask<Result<JsonObject>> Handle(GetModuleSecretSettingsQuery q, CancellationToken ct)
	{
		if (catalog.Find(q.ModuleId) is null) return Result.Failure<JsonObject>(ModuleCatalog.Unknown);
		if ((await currentUser.GetAsync())?.Module != q.ModuleId) return Result.Failure<JsonObject>(OwnSecretsOnly);
		ModuleSection? section = (await settings.GetAsync(ct)).Modules.GetValueOrDefault(q.ModuleId);
		return Result.Success(ModuleSettingsJson.Copy(section?.Settings));
	}
}
