using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Modules.Dtos;
using PrintDeck.Application.Features.Modules.Queries;
using PrintDeck.Application.Plugins;
using PrintDeck.Domain.Settings;
using PrintDeck.Plugins;

namespace PrintDeck.Application.Features.Modules.Handlers;

public sealed class GetModuleSettingsHandler(ModuleCatalog catalog, ISettingsStore settings)
	: IQueryHandler<GetModuleSettingsQuery, ModuleSettingsDto?>
{
	public async ValueTask<ModuleSettingsDto?> Handle(GetModuleSettingsQuery q, CancellationToken ct)
	{
		if (catalog.Find(q.ModuleId) is not { } module) return null;
		ModuleSection section = (await settings.GetAsync(ct)).Modules.GetValueOrDefault(module.Id) ?? new ModuleSection();
		return new ModuleSettingsDto(
			section.Enabled,
			ModuleSettingsJson.WithoutSecrets(section.Settings, module.SettingsType),
			ModuleSettingsJson.SavedSecrets(section.Settings, module.SettingsType));
	}
}
