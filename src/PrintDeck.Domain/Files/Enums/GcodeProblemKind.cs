namespace PrintDeck.Domain.Files.Enums;

public enum GcodeProblemKind
{
	BinaryFile,
	UnreplacedPlaceholder,
	BreaksStreaming,
	LineTooLong,
	OutsideBuildVolume,
	NoHotendTemperature,
	NoMoves
}
