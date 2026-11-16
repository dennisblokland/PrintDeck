using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.HostControl.Enums;

namespace PrintDeck.Application.Features.HostControl.Commands;

/// <summary>Restarts PrintDeck, or reboots or shuts down the host. Admin accounts only, never the slicer API key.</summary>
public sealed record RunHostActionCommand(HostAction Action) : ICommand<Result>, IHostAdministration;
