using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Logs.Dtos;

namespace PrintDeck.Application.Features.Logs.Queries;

/// <summary>Log files and whether the serial log is on. Logs can hold anything the host logged: admin accounts only.</summary>
public sealed record GetLogsQuery : IQuery<LogsDto>, IHostAdministration;
