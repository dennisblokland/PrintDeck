using PrintDeck.Domain.Files.Enums;

namespace PrintDeck.Domain.Files;

/// <summary>Something known to go wrong when the file is printed, with where it first shows up.</summary>
/// <param name="FirstLine">1-based line number of the first occurrence; null for whole-file problems.</param>
/// <param name="Count">How many lines have it.</param>
public sealed record GcodeProblem(GcodeProblemKind Kind, GcodeProblemSeverity Severity, string Message, long? FirstLine, int Count = 1);
