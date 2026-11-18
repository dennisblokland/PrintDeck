using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Commands;

/// <summary>Sets one control of the chosen camera and remembers the value for the next start.</summary>
public sealed record SetCameraControlCommand(string Name, long Value) : ICommand<Result>;
