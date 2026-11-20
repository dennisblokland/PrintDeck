using System.Text.Json.Nodes;

using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Modules.Commands;

/// <summary>Saves a module's settings. A secret left empty keeps its saved value.</summary>
public sealed record SaveModuleSettingsCommand(string ModuleId, JsonObject Settings) : ICommand<Result>
{
	/// <summary>Leaves the settings out: they may hold secrets, and a command can end up in a log.</summary>
	public override string ToString() => $"{nameof(SaveModuleSettingsCommand)} {{ {nameof(ModuleId)} = {ModuleId} }}";
}
