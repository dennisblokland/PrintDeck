using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Features.HostControl.Commands;
using PrintDeck.Application.Features.HostControl.Handlers;
using PrintDeck.Application.Features.HostControl.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.HostControl.Enums;
using PrintDeck.Domain.Printer.Enums;

namespace PrintDeck.Application.Tests.Features.HostControl;

public class HostControlHandlerTests
{
	private readonly FakePrinterSession _session = new() { State = PrinterState.Offline };
	private readonly FakeHostCommands _commands = new(HostAction.RestartPrintDeck, HostAction.RebootHost);
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Run(HostAction action) =>
		new RunHostActionHandler(_commands, _session, new FakeCurrentUser(FakeCurrentUser.Admin), NullLogger<RunHostActionHandler>.Instance)
			.Handle(new RunHostActionCommand(action), Ct);

	[Fact]
	public async Task Lists_the_configured_actions()
	{
		IReadOnlyList<HostAction> actions = await new GetHostActionsHandler(_commands).Handle(new GetHostActionsQuery(), Ct);
		Assert.Equal([HostAction.RestartPrintDeck, HostAction.RebootHost], actions);
	}

	[Fact]
	public async Task Runs_a_configured_action()
	{
		Assert.True((await Run(HostAction.RebootHost)).IsSuccess);
		Assert.Equal([HostAction.RebootHost], _commands.Ran);
	}

	[Fact]
	public async Task Refuses_an_action_without_a_command()
	{
		Assert.Equal("This action has no command configured.", (await Run(HostAction.ShutdownHost)).Error);
		Assert.Empty(_commands.Ran);
	}

	[Theory]
	[InlineData(PrinterState.Printing)]
	[InlineData(PrinterState.Paused)]
	[InlineData(PrinterState.Transferring)]
	public async Task Refuses_while_the_printer_is_busy(PrinterState state)
	{
		_session.State = state;
		Assert.Equal("Finish or cancel the print first.", (await Run(HostAction.RebootHost)).Error);
		Assert.Empty(_commands.Ran);
		Assert.False(_session.Disconnected);
	}

	[Fact]
	public async Task Disconnects_the_printer_first()
	{
		_session.State = PrinterState.Operational;
		await Run(HostAction.RestartPrintDeck);
		Assert.True(_session.Disconnected);
		Assert.Equal([HostAction.RestartPrintDeck], _commands.Ran);
	}

	[Fact]
	public async Task Passes_on_a_failed_command()
	{
		_commands.Outcome = Result.Failure("The command failed (exit code 1).");
		Assert.Equal("The command failed (exit code 1).", (await Run(HostAction.RebootHost)).Error);
	}
}
