using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.HostControl.Enums;

namespace PrintDeck.Infrastructure.HostControl;

/// <summary>Runs the configured commands through <c>/bin/sh -c</c> (or <c>cmd /c</c> on Windows).</summary>
public sealed class ShellHostCommands(IOptions<HostCommandOptions> options, ILogger<ShellHostCommands> logger) : IHostCommands
{
	/// <summary>
	/// How long to watch the command for an early failure. A working restart or reboot usually takes PrintDeck
	/// down before it ends, so still running after this counts as started.
	/// </summary>
	public TimeSpan StartupWindow { get; init; } = TimeSpan.FromSeconds(3);

	public IReadOnlyList<HostAction> Available =>
		Enum.GetValues<HostAction>().Where(a => !string.IsNullOrWhiteSpace(CommandFor(a))).ToList();

	public async Task<Result> RunAsync(HostAction action, CancellationToken ct)
	{
		string? command = CommandFor(action);
		if (string.IsNullOrWhiteSpace(command)) return Result.Failure("This action has no command configured.");

		ProcessStartInfo start = OperatingSystem.IsWindows()
			? new("cmd.exe") { ArgumentList = { "/c", command } }
			: new("/bin/sh") { ArgumentList = { "-c", command } };
		start.RedirectStandardError = true;
		start.RedirectStandardOutput = true;
		start.UseShellExecute = false;

		logger.LogWarning("Running {Action}: {Command}", action, command);
		Process process;
		try
		{
			process = Process.Start(start) ?? throw new InvalidOperationException("No process started.");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "{Action} could not start", action);
			return Result.Failure($"Could not run the command: {ex.Message}");
		}

		using (process)
		{
			Task<string> error = process.StandardError.ReadToEndAsync(CancellationToken.None);
			_ = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
			using CancellationTokenSource window = CancellationTokenSource.CreateLinkedTokenSource(ct);
			window.CancelAfter(StartupWindow);
			try
			{
				await process.WaitForExitAsync(window.Token);
			}
			catch (OperationCanceledException)
			{
				return Result.Success(); // still going: the host is busy restarting
			}

			if (process.ExitCode == 0) return Result.Success();

			string message = (await error).Trim();
			logger.LogError("{Action} failed with exit code {Code}: {Error}", action, process.ExitCode, message);
			return Result.Failure(message.Length > 0
				? $"The command failed (exit code {process.ExitCode}): {message}"
				: $"The command failed (exit code {process.ExitCode}).");
		}
	}

	private string? CommandFor(HostAction action) => action switch
	{
		HostAction.RestartPrintDeck => options.Value.RestartCommand,
		HostAction.RebootHost => options.Value.RebootCommand,
		HostAction.ShutdownHost => options.Value.ShutdownCommand,
		_ => null
	};
}
