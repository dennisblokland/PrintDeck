using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Backup;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>Restores <see cref="Restores"/> into <paramref name="settings"/> when <see cref="Outcome"/> succeeds.</summary>
internal sealed class FakeBackupArchive(FakeSettingsStore settings) : IBackupArchive
{
	public Result<BackupRestore> Outcome { get; set; } =
		Result.Success(new BackupRestore(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero), ["cube.gcode"]));

	public HostSettings Restores { get; set; } = new() { ApiKey = "KEY", SerialLogEnabled = true };
	public int Restored { get; private set; }

	public Task WriteAsync(Stream destination, CancellationToken ct) => destination.WriteAsync("zip"u8.ToArray(), ct).AsTask();

	public async Task<Result<BackupRestore>> RestoreAsync(Stream archive, CancellationToken ct)
	{
		Restored++;
		if (Outcome.IsSuccess) await settings.UpdateAsync(_ => Restores, ct);
		return Outcome;
	}
}
