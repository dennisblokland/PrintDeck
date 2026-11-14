using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Jobs.Handlers;

public sealed class StartPrintHandler(IPrinterSession session, IGcodeFileStore files) : ICommandHandler<StartPrintCommand, Result>
{
	public async ValueTask<Result> Handle(StartPrintCommand c, CancellationToken ct)
	{
		GcodeFile? file = await files.GetAsync(c.FileName, ct);
		if (file is null) return Result.Failure($"File {c.FileName} not found.");
		if (file.SizeBytes == 0) return Result.Failure("File is empty.");
		return session.StartJob(file.Name, files.OpenRead(file.Name), file.SizeBytes, file.Analysis?.EstimatedPrintTime);
	}
}
