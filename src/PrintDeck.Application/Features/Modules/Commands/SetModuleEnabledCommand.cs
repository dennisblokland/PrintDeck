using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Modules.Commands;

/// <summary>Switches a module on or off; it starts or stops right away.</summary>
public sealed record SetModuleEnabledCommand(string ModuleId, bool Enabled) : ICommand<Result>;
