using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Settings.Commands;

/// <summary>Makes a saved profile the active one.</summary>
public sealed record SelectPrinterProfileCommand(string Id) : ICommand<Result>;
