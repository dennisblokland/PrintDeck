namespace PrintDeck.Infrastructure.Processes;

/// <summary>Runs a program with arguments (no shell); null when the program isn't installed.</summary>
public interface ICommandRunner
{
	Task<CommandOutput?> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct);
}
