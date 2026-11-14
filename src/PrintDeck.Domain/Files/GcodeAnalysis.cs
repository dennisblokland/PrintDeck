namespace PrintDeck.Domain.Files;

public sealed record GcodeAnalysis(
	TimeSpan? EstimatedPrintTime,
	double? FilamentMillimeters,
	int? LayerCount,
	long CommandCount,
	string? Slicer);
