using PrintDeck.Domain.Backup;

namespace PrintDeck.Domain.Tests.Backup;

public class BackupLayoutTests
{
	[Fact]
	public void File_name_carries_the_utc_date_and_time()
	{
		DateTimeOffset at = new(2026, 11, 17, 16, 30, 0, TimeSpan.FromHours(1));
		Assert.Equal("printdeck-backup-20261117-1530.zip", BackupLayout.FileName(at));
	}

	[Theory]
	[InlineData("uploads/cube.gcode", "cube.gcode")]
	[InlineData("uploads/cube.gcode.meta.json", "cube.gcode.meta.json")]
	[InlineData("uploads/my part (2).gcode", "my part (2).gcode")]
	public void Plain_files_under_uploads_are_uploads(string entry, string name)
	{
		Assert.Equal(name, BackupLayout.UploadName(entry));
	}

	[Theory]
	[InlineData("settings.json")]
	[InlineData("uploads/")]
	[InlineData("uploads/../settings.json")]
	[InlineData("uploads/..")]
	[InlineData("uploads/sub/cube.gcode")]
	[InlineData("uploads/..\\cube.gcode")]
	[InlineData("uploads/.hidden")]
	[InlineData("uploads/cube.gcode.part")]
	[InlineData("Uploads/cube.gcode")]
	public void Anything_else_is_not(string entry)
	{
		Assert.Null(BackupLayout.UploadName(entry));
	}

	[Theory]
	[InlineData("cube.gcode.meta.json", "cube.gcode")]
	[InlineData("cube.gcode.prints.json", "cube.gcode")]
	[InlineData("cube.gcode", null)]
	[InlineData(".meta.json", null)]
	public void Sidecars_name_their_upload(string name, string? owner)
	{
		Assert.Equal(owner, BackupLayout.SidecarOwner(name));
	}
}
