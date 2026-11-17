using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Files.Thumbnails;
using PrintDeck.Application.Tests.TestDoubles;

using static PrintDeck.Application.Tests.TestDoubles.ThumbnailSamples;

namespace PrintDeck.Application.Tests.Files.Thumbnails;

public class ThumbnailBackfillTests
{
	private readonly InMemoryGcodeFileStore _files = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();

	private Task<int> Run() =>
		new ThumbnailBackfill(_files, _events.Hub, NullLogger<ThumbnailBackfill>.Instance).RunAsync(TestContext.Current.CancellationToken);

	[Fact]
	public async Task Adds_thumbnails_to_files_that_have_none()
	{
		_files.Add("orca.gcode", Block("thumbnail", 160, 120, Png(160, 120)) + "G28\n");
		_files.Add("plain.gcode", "G28\n");

		Assert.Equal(1, await Run());

		Assert.Equal(160, _files.Thumbnails["orca.gcode"].Width);
		Assert.False(_files.Thumbnails.ContainsKey("plain.gcode"));
		Assert.Contains(PrinterChange.Files, _events.Published);
	}

	[Fact]
	public async Task Leaves_existing_thumbnails_alone()
	{
		_files.Add("orca.gcode", Block("thumbnail", 160, 120, Png(160, 120)) + "G28\n");
		await Run();
		_events.Published.Clear();

		Assert.Equal(0, await Run());
		Assert.Empty(_events.Published);
	}
}
