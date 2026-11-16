using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.SdCard.Commands;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.SdCard.Handlers;

public sealed class CopyToSdCardHandler(IPrinterSession session, IGcodeFileStore files) : ICommandHandler<CopyToSdCardCommand, Result<string>>
{
	public async ValueTask<Result<string>> Handle(CopyToSdCardCommand c, CancellationToken ct)
	{
		GcodeFile? file = await files.GetAsync(c.FileName, ct);
		if (file is null) return Result.Failure<string>($"File {c.FileName} not found.");
		if (file.SizeBytes == 0) return Result.Failure<string>("File is empty.");
		return session.StartSdUpload(file.Name, files.OpenRead(file.Name), file.SizeBytes);
	}
}
