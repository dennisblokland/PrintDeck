using System.Text.Json.Serialization;

using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Domain.Files;

/// <summary>One finished print of a stored file.</summary>
/// <param name="PrintTime">Time spent printing, pauses left out; zero in records written before it was kept.</param>
/// <param name="FilamentMm">Filament used: the file's total for a completed print, the printed share otherwise; null when unknown.</param>
public sealed record PrintRecord(
	DateTimeOffset StartedAt,
	DateTimeOffset EndedAt,
	JobStatus Status,
	TimeSpan PrintTime = default,
	double? FilamentMm = null)
{
	[JsonIgnore]
	public bool Succeeded => Status == JobStatus.Completed;
}
