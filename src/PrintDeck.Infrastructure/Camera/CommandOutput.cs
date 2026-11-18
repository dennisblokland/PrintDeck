namespace PrintDeck.Infrastructure.Camera;

public sealed record CommandOutput(int ExitCode, string Output, string Error);
