using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Logs.Queries;

namespace PrintDeck.Application.Features.Logs.Handlers;

public sealed class OpenLogFileHandler(ILogFiles files) : IQueryHandler<OpenLogFileQuery, Stream?>
{
	public ValueTask<Stream?> Handle(OpenLogFileQuery query, CancellationToken ct) => ValueTask.FromResult(files.OpenRead(query.Name));
}
