using Mediator;

using PrintDeck.Application.Features.SdCard.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Handlers;

public sealed class DeleteSdFileHandler(IPrinterSession session) : ICommandHandler<DeleteSdFileCommand, Result>
{
	public ValueTask<Result> Handle(DeleteSdFileCommand c, CancellationToken ct) => ValueTask.FromResult(session.DeleteSdFile(c.Path));
}
