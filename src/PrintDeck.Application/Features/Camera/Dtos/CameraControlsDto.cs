using PrintDeck.Domain.Camera;

namespace PrintDeck.Application.Features.Camera.Dtos;

/// <param name="Device">The chosen camera, or null for none.</param>
/// <param name="Devices">Cameras that can be chosen.</param>
/// <param name="Error">Why the controls could not be read, e.g. the camera is unplugged.</param>
public sealed record CameraControlsDto(string? Device, IReadOnlyList<string> Devices, IReadOnlyList<CameraControl> Controls, string? Error);
