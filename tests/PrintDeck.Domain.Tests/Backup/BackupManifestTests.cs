using PrintDeck.Domain.Backup;

namespace PrintDeck.Domain.Tests.Backup;

public class BackupManifestTests
{
	private static readonly DateTimeOffset _at = new(2026, 11, 17, 15, 30, 0, TimeSpan.Zero);

	[Fact]
	public void A_new_manifest_can_be_restored()
	{
		BackupManifest manifest = BackupManifest.Create(_at, 3);
		Assert.Null(manifest.Problem());
		Assert.Equal(3, manifest.FileCount);
	}

	[Fact]
	public void Another_format_is_not_a_backup()
	{
		Assert.Equal("That file is not a PrintDeck backup.", new BackupManifest("octoprint", 1, _at, 0).Problem());
	}

	[Fact]
	public void A_newer_version_needs_a_newer_printdeck()
	{
		BackupManifest manifest = BackupManifest.Create(_at, 0) with { Version = BackupManifest.CurrentVersion + 1 };
		Assert.Equal("This backup was made by a newer PrintDeck. Update PrintDeck first.", manifest.Problem());
	}
}
