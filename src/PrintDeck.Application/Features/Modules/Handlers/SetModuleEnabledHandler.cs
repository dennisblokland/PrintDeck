using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Modules.Commands;
using PrintDeck.Application.Plugins;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Modules.Handlers;

public sealed class SetModuleEnabledHandler(ModuleCatalog catalog, ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<SetModuleEnabledCommand, Result>
{
	public async ValueTask<Result> Handle(SetModuleEnabledCommand c, CancellationToken ct)
	{
		if (catalog.Find(c.ModuleId) is null) return Result.Failure(ModuleCatalog.Unknown);
		await settings.UpdateAsync(s =>
		{
			Dictionary<string, ModuleSection> modules = new(s.Modules);
			modules[c.ModuleId] = (modules.GetValueOrDefault(c.ModuleId) ?? new ModuleSection()) with { Enabled = c.Enabled };
			return s with { Modules = modules };
		}, ct);
		events.Publish(PrinterChange.Settings); // the module host starts or stops it
		return Result.Success();
	}
}
