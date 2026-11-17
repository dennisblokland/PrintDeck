using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Backup.Dtos;
using PrintDeck.Application.Features.Backup.Queries;
using PrintDeck.Domain.Backup;

namespace PrintDeck.Application.Features.Backup.Handlers;

public sealed class GetBackupHandler(IBackupArchive archive, TimeProvider time) : IQueryHandler<GetBackupQuery, BackupDownload>
{
	public ValueTask<BackupDownload> Handle(GetBackupQuery query, CancellationToken ct) =>
		ValueTask.FromResult(new BackupDownload(BackupLayout.FileName(time.GetUtcNow()), archive.WriteAsync));
}
