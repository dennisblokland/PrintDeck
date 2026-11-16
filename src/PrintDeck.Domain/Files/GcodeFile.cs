namespace PrintDeck.Domain.Files;

/// <param name="Prints">How often it printed; null when the store does not keep history.</param>
public sealed record GcodeFile(string Name, long SizeBytes, DateTimeOffset UploadedAt, GcodeAnalysis? Analysis, PrintStats? Prints = null);
