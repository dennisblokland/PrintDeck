using Mediator;

using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Backup.Commands;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Backup;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Backup.Handlers;

public sealed class RestoreBackupHandler(
	IBackupArchive archive,
	IPrinterSession session,
	ISettingsStore settings,
	ISerialLog serialLog,
	ToolpathCache toolpaths,
	PrinterEventHub events,
	ILogger<RestoreBackupHandler> logger) : ICommandHandler<RestoreBackupCommand, Result<BackupRestore>>
{
	public async ValueTask<Result<BackupRestore>> Handle(RestoreBackupCommand c, CancellationToken ct)
	{
		// A restore can overwrite the file being printed and switch the active profile under the job.
		if (session.State.IsBusy()) return Result.Failure<BackupRestore>("Finish or cancel the print first.");

		Result<BackupRestore> result = await archive.RestoreAsync(c.Archive, ct);
		if (!result.IsSuccess) return result;

		BackupRestore restored = result.Value!;
		foreach (string name in restored.Files) toolpaths.Invalidate(name);
		HostSettings now = await settings.GetAsync(ct);
		serialLog.SetEnabled(now.SerialLogEnabled);
		logger.LogInformation("Restored settings and {Count} files from a backup made {CreatedAt:u}", restored.Files.Count, restored.CreatedAt);

		events.Publish(PrinterChange.Settings | PrinterChange.Files);
		return result;
	}
}
