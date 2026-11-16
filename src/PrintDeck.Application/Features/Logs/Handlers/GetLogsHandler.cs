using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Logs.Dtos;
using PrintDeck.Application.Features.Logs.Queries;

namespace PrintDeck.Application.Features.Logs.Handlers;

public sealed class GetLogsHandler(ILogFiles files, ISerialLog serialLog) : IQueryHandler<GetLogsQuery, LogsDto>
{
	public ValueTask<LogsDto> Handle(GetLogsQuery query, CancellationToken ct) =>
		ValueTask.FromResult(new LogsDto(serialLog.Enabled, files.List()));
}
