namespace PrintDeck.Domain.Backup;

/// <summary>What a restore put back: the settings (always) and these uploads, from a backup made at <paramref name="CreatedAt"/>.</summary>
public sealed record BackupRestore(DateTimeOffset CreatedAt, IReadOnlyList<string> Files);
