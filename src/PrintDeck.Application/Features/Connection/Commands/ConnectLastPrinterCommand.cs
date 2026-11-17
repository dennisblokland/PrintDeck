using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Connection.Commands;

/// <summary>
/// Makes sure the printer is connected: succeeds at once when it already is, waits for a connect that is under way,
/// and otherwise connects to the last used port and baud rate.
/// </summary>
public sealed record ConnectLastPrinterCommand : ICommand<Result>;
