using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Files.Thumbnails;

/// <summary>
/// Gives files uploaded before thumbnails existed (or restored from an older backup) their slicer thumbnail.
/// Runs once at startup; only the header of each file is read, so files without a thumbnail cost little to check again.
/// </summary>
public sealed class ThumbnailBackfill(IGcodeFileStore files, PrinterEventHub events, ILogger<ThumbnailBackfill> logger)
{
	public Task Start() => Task.Run(() => RunAsync(CancellationToken.None));

	public async Task<int> RunAsync(CancellationToken ct)
	{
		int added = 0;
		foreach (GcodeFile file in await files.ListAsync(ct))
		{
			if (file.HasThumbnail) continue;
			try
			{
				GcodeThumbnail? thumbnail;
				await using (Stream stream = files.OpenRead(file.Name))
					thumbnail = await GcodeThumbnailReader.ReadAsync(stream, ct);
				if (thumbnail is null) continue;
				await files.SaveThumbnailAsync(file.Name, thumbnail, ct);
				added++;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				logger.LogWarning(ex, "Could not read the thumbnail of {File}", file.Name);
			}
		}

		if (added > 0)
		{
			logger.LogInformation("Added slicer thumbnails to {Count} stored files", added);
			events.Publish(PrinterChange.Files);
		}
		return added;
	}
}
