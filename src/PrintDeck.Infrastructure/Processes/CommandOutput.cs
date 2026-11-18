namespace PrintDeck.Infrastructure.Processes;

public sealed record CommandOutput(int ExitCode, string Output, string Error);
