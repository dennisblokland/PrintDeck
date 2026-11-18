using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Commands;

/// <summary>Puts every control of the chosen camera back to its default and forgets the saved values.</summary>
public sealed record ResetCameraControlsCommand : ICommand<Result>;
