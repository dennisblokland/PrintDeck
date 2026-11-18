using System.ComponentModel;
using System.Diagnostics;

namespace PrintDeck.Infrastructure.Camera;

public sealed class ProcessCommandRunner : ICommandRunner
{
	public async Task<CommandOutput?> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
	{
		ProcessStartInfo start = new(program)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};
		foreach (string a in arguments) start.ArgumentList.Add(a);

		Process process;
		try
		{
			process = Process.Start(start) ?? throw new InvalidOperationException("No process started.");
		}
		catch (Win32Exception)
		{
			return null; // not installed
		}

		using (process)
		{
			Task<string> output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
			Task<string> error = process.StandardError.ReadToEndAsync(CancellationToken.None);
			using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
			limit.CancelAfter(timeout);
			try
			{
				await process.WaitForExitAsync(limit.Token);
			}
			catch (OperationCanceledException)
			{
				try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { /* already gone */ }
				throw new TimeoutException($"{program} did not finish in time.");
			}
			return new CommandOutput(process.ExitCode, await output, await error);
		}
	}
}
