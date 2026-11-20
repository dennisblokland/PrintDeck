using System.Text.Json;
using System.Text.Json.Nodes;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Modules.Commands;
using PrintDeck.Application.Plugins;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;
using PrintDeck.Plugins;

namespace PrintDeck.Application.Features.Modules.Handlers;

public sealed class SaveModuleSettingsHandler(ModuleCatalog catalog, ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<SaveModuleSettingsCommand, Result>
{
	public const string DoesNotFit = "Those settings don't fit the module.";

	public async ValueTask<Result> Handle(SaveModuleSettingsCommand c, CancellationToken ct)
	{
		if (catalog.Find(c.ModuleId) is not { } module) return Result.Failure(ModuleCatalog.Unknown);
		if (module.SettingsType is not { } type) return Result.Failure(DoesNotFit);
		try { _ = c.Settings.Deserialize(type, ModuleSettingsJson.Options); }
		catch (JsonException) { return Result.Failure(DoesNotFit); }

		await settings.UpdateAsync(s =>
		{
			Dictionary<string, ModuleSection> modules = new(s.Modules);
			ModuleSection section = modules.GetValueOrDefault(c.ModuleId) ?? new ModuleSection();
			JsonObject merged = ModuleSettingsJson.KeepSavedSecrets(c.Settings, section.Settings, type);
			modules[c.ModuleId] = section with { Settings = merged };
			return s with { Modules = modules };
		}, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
