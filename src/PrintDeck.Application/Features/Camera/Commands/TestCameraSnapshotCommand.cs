using Mediator;

using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Commands;

/// <summary>
/// Takes a snapshot from a URL that is not saved yet, so Settings can try it first.
/// A command, so only admins can make PrintDeck fetch an address of their choosing.
/// </summary>
public sealed record TestCameraSnapshotCommand(string Url) : ICommand<Result<CameraSnapshot>>;
