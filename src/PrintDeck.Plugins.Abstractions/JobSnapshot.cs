namespace PrintDeck.Plugins;

/// <param name="Progress">0 to 1.</param>
/// <param name="IsActive">Printing or paused; false once it ended.</param>
public sealed record JobSnapshot(string FileName, double Progress, bool IsActive, bool IsPaused, bool FromSdCard);
