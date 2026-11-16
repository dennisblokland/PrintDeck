using Mediator;

using PrintDeck.Application.Features.Files.Dtos;

namespace PrintDeck.Application.Features.Files.Queries;

/// <summary>Finished prints of stored files, newest first, with totals. A deleted file takes its history with it.</summary>
public sealed record GetPrintHistoryQuery(int Limit = 100) : IQuery<PrintHistoryDto>;
