using PrintDeck.Domain.Common;
using PrintDeck.Domain.HostControl.Enums;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>
/// Runs the restart / reboot / shutdown commands configured on the host.
/// Only actions with a configured command are available.
/// </summary>
public interface IHostCommands
{
	IReadOnlyList<HostAction> Available { get; }

	Task<Result> RunAsync(HostAction action, CancellationToken ct);
}
