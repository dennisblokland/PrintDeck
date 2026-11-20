using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Modules.Dtos;
using PrintDeck.Application.Features.Modules.Queries;
using PrintDeck.Application.Plugins;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Modules.Handlers;

public sealed class ListModulesHandler(ModuleCatalog catalog, ModuleHost host, ISettingsStore settings)
	: IQueryHandler<ListModulesQuery, IReadOnlyList<ModuleDto>>
{
	public async ValueTask<IReadOnlyList<ModuleDto>> Handle(ListModulesQuery q, CancellationToken ct)
	{
		HostSettings s = await settings.GetAsync(ct);
		return catalog.All
			.Select(m => new ModuleDto(m.Id, m.Name, m.Version.ToString(),
				s.Modules.TryGetValue(m.Id, out ModuleSection? section) && section.Enabled, host.IsRunning(m.Id), m.Components))
			.ToList();
	}
}
