using Mediator;

using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Queries;

/// <summary>A still image from the saved snapshot URL.</summary>
public sealed record GetCameraSnapshotQuery : IQuery<Result<CameraSnapshot>>;
