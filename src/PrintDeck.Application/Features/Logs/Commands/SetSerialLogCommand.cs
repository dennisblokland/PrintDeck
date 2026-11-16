using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Logs.Commands;

/// <summary>Turns the full serial log on or off; remembered across restarts.</summary>
public sealed record SetSerialLogCommand(bool Enabled) : ICommand<Result>, IHostAdministration;
