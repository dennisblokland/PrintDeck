using System.Text;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Tests.Features.Files;

public class UploadGcodeHandlerTests
{
	private readonly InMemoryGcodeFileStore _files = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();

	private ValueTask<Result<GcodeFile>> Upload(string name, string content = "G28\nG1 X10\n") =>
		new UploadGcodeHandler(_files, _events.Hub).Handle(
			new UploadGcodeCommand(name, new MemoryStream(Encoding.UTF8.GetBytes(content))), TestContext.Current.CancellationToken);

	[Theory]
	[InlineData("benchy.gcode", "benchy.gcode")]
	[InlineData("  benchy.gcode  ", "benchy.gcode")]
	[InlineData("../../etc/passwd", "passwd")]
	[InlineData("C:\\models\\cube.gcode", "cube.gcode")]
	[InlineData(".hidden", null)]
	[InlineData("dir/", null)]
	[InlineData("   ", null)]
	public void Names_are_sanitised(string raw, string? expected) =>
		Assert.Equal(expected, UploadGcodeHandler.SanitizeName(raw));

	[Fact]
	public async Task Stores_the_file_with_its_analysis()
	{
		Result<GcodeFile> r = await Upload("../cube.gcode");

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal("cube.gcode", r.Value!.Name);
		Assert.Equal(2, r.Value.Analysis?.CommandCount);
		Assert.Equal(2, (await _files.GetAsync("cube.gcode", TestContext.Current.CancellationToken))!.Analysis?.CommandCount);
		Assert.Contains(PrinterChange.Files, _events.Published);
	}

	[Theory]
	[InlineData("part.GCODE")]
	[InlineData("part.gco")]
	[InlineData("part.g")]
	[InlineData("part.bgcode")]
	public async Task Accepts_gcode_extensions(string name)
	{
		Assert.True((await Upload(name)).IsSuccess);
	}

	[Theory]
	[InlineData("model.stl")]
	[InlineData("gcode")]
	public async Task Rejects_other_files(string name)
	{
		Result<GcodeFile> r = await Upload(name);
		Assert.Equal("Only G-code files (.gcode, .gco, .g) can be uploaded.", r.Error);
		Assert.False(_files.Contains(name));
		Assert.Empty(_events.Published);
	}

	[Fact]
	public async Task Rejects_invalid_names()
	{
		Assert.Equal("Invalid file name.", (await Upload(".gcode")).Error);
	}
}
