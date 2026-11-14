using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class DeleteGcodeHandler(IGcodeFileStore files, IPrinterSession session, PrinterEventHub events, ToolpathCache toolpaths)
	: ICommandHandler<DeleteGcodeCommand, Result>
{
	public async ValueTask<Result> Handle(DeleteGcodeCommand c, CancellationToken ct)
	{
		if (session.CurrentJob is { IsActive: true } job && job.FileName == c.FileName)
			return Result.Failure("Can't delete the file that is printing.");
		if (!await files.DeleteAsync(c.FileName, ct)) return Result.Failure("File not found.");
		toolpaths.Invalidate(c.FileName);
		events.Publish(PrinterChange.Files);
		return Result.Success();
	}
}
