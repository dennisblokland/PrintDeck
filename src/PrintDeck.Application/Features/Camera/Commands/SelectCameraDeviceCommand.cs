using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Camera.Commands;

/// <summary>Chooses the camera whose controls PrintDeck adjusts; null or empty for none. Another camera starts without saved values.</summary>
public sealed record SelectCameraDeviceCommand(string? Device) : ICommand<Result>;
