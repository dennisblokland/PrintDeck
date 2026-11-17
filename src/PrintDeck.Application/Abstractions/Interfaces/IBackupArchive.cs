using PrintDeck.Domain.Backup;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Writes and restores backup archives: settings with printer profiles, plus every stored upload and its sidecars.</summary>
public interface IBackupArchive
{
	/// <summary>Streams a new archive into <paramref name="destination"/>, which need not be seekable.</summary>
	Task WriteAsync(Stream destination, CancellationToken ct);

	/// <summary>
	/// Checks the whole archive first, then replaces the settings and writes its uploads, overwriting files with the
	/// same name. Other uploads stay. Nothing changes when the archive is refused.
	/// </summary>
	Task<Result<BackupRestore>> RestoreAsync(Stream archive, CancellationToken ct);
}
