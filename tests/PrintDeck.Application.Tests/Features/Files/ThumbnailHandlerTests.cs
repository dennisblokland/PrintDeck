using PrintDeck.Application.Features.Files.Dtos;
using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Tests.Features.Files;

public class ThumbnailHandlerTests
{
	private readonly InMemoryGcodeFileStore _files = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<ThumbnailContent?> Thumbnail(string name) =>
		new GetGcodeThumbnailHandler(_files).Handle(new GetGcodeThumbnailQuery(name), Ct);

	[Fact]
	public async Task Opens_the_stored_thumbnail()
	{
		_files.Add("orca.gcode", "G28\n");
		byte[] png = ThumbnailSamples.Png(8, 6);
		await _files.SaveThumbnailAsync("orca.gcode", new GcodeThumbnail(8, 6, GcodeThumbnail.Png, png), Ct);

		ThumbnailContent? t = await Thumbnail("orca.gcode");

		Assert.NotNull(t);
		await using Stream content = t.Content;
		using MemoryStream copy = new();
		await content.CopyToAsync(copy, Ct);
		Assert.Equal(png, copy.ToArray());
		Assert.Equal(GcodeThumbnail.Png, t.ContentType);
		Assert.Equal("orca.gcode", t.File.Name);
	}

	[Theory]
	[InlineData("missing.gcode")]
	[InlineData("plain.gcode")]
	[InlineData("../plain.gcode")]
	public async Task Is_null_without_a_thumbnail(string name)
	{
		_files.Add("plain.gcode", "G28\n");

		Assert.Null(await Thumbnail(name));
	}

	[Fact]
	public async Task Gets_one_file()
	{
		_files.Add("plain.gcode", "G28\n");
		GetGcodeFileHandler handler = new(_files);

		Assert.Equal("plain.gcode", (await handler.Handle(new GetGcodeFileQuery("plain.gcode"), Ct))?.Name);
		Assert.Null(await handler.Handle(new GetGcodeFileQuery("missing.gcode"), Ct));
		Assert.Null(await handler.Handle(new GetGcodeFileQuery("../plain.gcode"), Ct));
	}
}
