namespace PrintDeck.Application.Features.Backup.Dtos;

/// <summary>A backup ready to stream: the archive is written while it downloads, so it never sits on disk twice.</summary>
public sealed record BackupDownload(string FileName, Func<Stream, CancellationToken, Task> WriteAsync);
