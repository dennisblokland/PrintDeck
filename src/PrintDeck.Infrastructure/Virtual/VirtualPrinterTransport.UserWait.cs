using System.Text.RegularExpressions;

namespace PrintDeck.Infrastructure.Virtual;

/// <summary>
/// M0/M1/M600 on the virtual printer: they block until the host answers the prompt (M876) or sends M108,
/// which the emergency parser picks up as soon as the line arrives, like Marlin's.
/// </summary>
public sealed partial class VirtualPrinterTransport
{
	private readonly Lock _waitGate = new();
	private TaskCompletionSource? _userWait;

	/// <summary>Emergency parser: acts on M108/M876 the moment they are written, before the queue gets to them.</summary>
	private void ParseEmergency(string wire)
	{
		Match m = Numbered().Match(wire.Trim());
		string command = (m.Success ? m.Groups["cmd"].Value : wire).Trim().ToUpperInvariant();
		if (command.StartsWith("M108", StringComparison.Ordinal) || (command.StartsWith("M876 S", StringComparison.Ordinal)))
			lock (_waitGate) _userWait?.TrySetResult();
	}

	private async Task WaitForUserAsync(string message, CancellationToken ct)
	{
		TaskCompletionSource wait = new(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (_waitGate) _userWait = wait;

		Emit("//action:prompt_end");
		Emit($"//action:prompt_begin {message}");
		Emit("//action:prompt_button Continue");
		Emit("//action:prompt_show");
		try
		{
			while (!wait.Task.IsCompleted)
			{
				Emit("echo:busy: paused for user");
				await Task.WhenAny(wait.Task, Task.Delay(TimeSpan.FromSeconds(2), time, ct));
				ct.ThrowIfCancellationRequested();
			}
		}
		finally
		{
			lock (_waitGate) _userWait = null;
		}
		Emit("//action:prompt_end");
	}
}
