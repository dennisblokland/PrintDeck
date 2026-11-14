namespace PrintDeck.Domain.Files;

public sealed record GcodeFile(string Name, long SizeBytes, DateTimeOffset UploadedAt, GcodeAnalysis? Analysis);
