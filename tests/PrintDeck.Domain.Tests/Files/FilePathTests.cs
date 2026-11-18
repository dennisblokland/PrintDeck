using PrintDeck.Domain.Files;

namespace PrintDeck.Domain.Tests.Files;

public class FilePathTests
{
	[Theory]
	[InlineData("", "")]
	[InlineData(null, "")]
	[InlineData("parts", "parts")]
	[InlineData(" parts / brackets ", "parts/brackets")]
	[InlineData("parts\\brackets\\cube.gcode", "parts/brackets/cube.gcode")]
	[InlineData("/parts//cube.gcode/", "parts/cube.gcode")]
	[InlineData("my part (2).gcode", "my part (2).gcode")]
	public void Normalizes(string? raw, string expected) => Assert.Equal(expected, FilePath.Normalize(raw));

	[Theory]
	[InlineData("..")]
	[InlineData("parts/../secrets")]
	[InlineData("./parts")]
	[InlineData(".hidden/cube.gcode")]
	[InlineData("parts/.git")]
	[InlineData("a/b/c/d/e/f/g/h/i")]
	public void Refuses(string raw) => Assert.Null(FilePath.Normalize(raw));

	[Fact]
	public void Refuses_overlong_parts() => Assert.Null(FilePath.Normalize(new string('a', FilePath.MaxSegmentLength + 1)));

	[Fact]
	public void Replaces_characters_a_file_name_cannot_have() => Assert.Equal("a_b", FilePath.Normalize("a\0b"));

	[Theory]
	[InlineData("cube.gcode", "", "cube.gcode")]
	[InlineData("parts/cube.gcode", "parts", "cube.gcode")]
	[InlineData("parts/brackets/cube.gcode", "parts/brackets", "cube.gcode")]
	public void Splits_folder_and_name(string path, string folder, string name)
	{
		Assert.Equal(folder, FilePath.Folder(path));
		Assert.Equal(name, FilePath.Name(path));
		Assert.Equal(path, FilePath.Combine(folder, name));
	}

	[Theory]
	[InlineData("parts/cube.gcode", "parts", true)]
	[InlineData("parts/brackets/cube.gcode", "parts", true)]
	[InlineData("parts", "parts", true)]
	[InlineData("anything", "", true)]
	[InlineData("parts2/cube.gcode", "parts", false)]
	[InlineData("cube.gcode", "parts", false)]
	public void Knows_what_is_within_a_folder(string path, string folder, bool within) =>
		Assert.Equal(within, FilePath.IsWithin(path, folder));
}
