using PrintDeck.Domain.Logs;

namespace PrintDeck.Application.Features.Logs.Dtos;

public sealed record LogsDto(bool SerialLogEnabled, IReadOnlyList<LogFile> Files);
