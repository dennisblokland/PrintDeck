using Mediator;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Commands;

/// <summary>Replaces the Control page's custom buttons, in the order given.</summary>
public sealed record SaveCustomButtonsCommand(IReadOnlyList<CustomButton> Buttons) : ICommand<Result>;
