namespace PrintDeck.Domain.Files;

/// <param name="Prints">How often it printed; null when the store does not keep history.</param>
/// <param name="HasThumbnail">The slicer embedded a preview image, stored next to the file.</param>
public sealed record GcodeFile(string Name, long SizeBytes, DateTimeOffset UploadedAt, GcodeAnalysis? Analysis, PrintStats? Prints = null, bool HasThumbnail = false);
