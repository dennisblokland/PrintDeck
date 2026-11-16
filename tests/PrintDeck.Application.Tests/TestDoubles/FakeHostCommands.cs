using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.HostControl.Enums;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeHostCommands(params HostAction[] available) : IHostCommands
{
	public IReadOnlyList<HostAction> Available { get; } = available;
	public List<HostAction> Ran { get; } = [];
	public Result Outcome { get; set; } = Result.Success();

	public Task<Result> RunAsync(HostAction action, CancellationToken ct)
	{
		Ran.Add(action);
		return Task.FromResult(Outcome);
	}
}
