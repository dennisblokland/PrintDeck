namespace PrintDeck.Domain.Timelapse;

/// <summary>A rendered timelapse: an MP4 in the timelapse folder.</summary>
public sealed record TimelapseVideo(string Name, long SizeBytes, DateTimeOffset CreatedAt);
