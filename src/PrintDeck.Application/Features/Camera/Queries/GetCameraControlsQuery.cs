using Mediator;

using PrintDeck.Application.Features.Camera.Dtos;

namespace PrintDeck.Application.Features.Camera.Queries;

/// <summary>The chosen camera device and its controls with their current values.</summary>
public sealed record GetCameraControlsQuery : IQuery<CameraControlsDto>;
