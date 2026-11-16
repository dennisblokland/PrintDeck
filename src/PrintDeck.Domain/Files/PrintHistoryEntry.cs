namespace PrintDeck.Domain.Files;

/// <summary>A finished print and the file it was.</summary>
public sealed record PrintHistoryEntry(string FileName, PrintRecord Print);
