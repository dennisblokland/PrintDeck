using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PrintDeck.Domain.Common;
using PrintDeck.Domain.HostControl.Enums;
using PrintDeck.Infrastructure.HostControl;

namespace PrintDeck.Infrastructure.Tests.HostControl;

/// <summary>Real processes through the platform shell; "exit n" works in both sh and cmd.</summary>
public class ShellHostCommandsTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static ShellHostCommands With(HostCommandOptions options, TimeSpan? window = null) =>
		new(Options.Create(options), NullLogger<ShellHostCommands>.Instance) { StartupWindow = window ?? TimeSpan.FromSeconds(10) };

	[Fact]
	public void Offers_only_configured_actions()
	{
		ShellHostCommands commands = With(new HostCommandOptions { RebootCommand = "exit 0", ShutdownCommand = "  " });
		Assert.Equal([HostAction.RebootHost], commands.Available);
		Assert.Empty(With(new HostCommandOptions()).Available);
	}

	[Fact]
	public async Task A_command_that_exits_cleanly_succeeds()
	{
		Result result = await With(new HostCommandOptions { RestartCommand = "exit 0" }).RunAsync(HostAction.RestartPrintDeck, Ct);
		Assert.True(result.IsSuccess, result.Error);
	}

	[Fact]
	public async Task A_failing_command_reports_its_exit_code_and_error()
	{
		string command = OperatingSystem.IsWindows() ? "echo not allowed 1>&2 & exit 3" : "echo not allowed >&2; exit 3";
		Result result = await With(new HostCommandOptions { RebootCommand = command }).RunAsync(HostAction.RebootHost, Ct);
		Assert.Equal("The command failed (exit code 3): not allowed", result.Error);
	}

	[Fact]
	public async Task A_command_still_running_after_the_window_counts_as_started()
	{
		string command = OperatingSystem.IsWindows() ? "ping -n 6 127.0.0.1 > nul" : "sleep 5";
		Result result = await With(new HostCommandOptions { ShutdownCommand = command }, TimeSpan.FromMilliseconds(200))
			.RunAsync(HostAction.ShutdownHost, Ct);
		Assert.True(result.IsSuccess, result.Error);
	}

	[Fact]
	public async Task An_unconfigured_action_is_refused()
	{
		Result result = await With(new HostCommandOptions()).RunAsync(HostAction.RebootHost, Ct);
		Assert.Equal("This action has no command configured.", result.Error);
	}
}
