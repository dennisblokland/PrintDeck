using System.Text.Json.Serialization;

using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Domain.Files;

/// <summary>One finished print of a stored file.</summary>
public sealed record PrintRecord(DateTimeOffset StartedAt, DateTimeOffset EndedAt, JobStatus Status)
{
	[JsonIgnore]
	public bool Succeeded => Status == JobStatus.Completed;
}
