using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Timelapse.Commands;
using PrintDeck.Application.Features.Timelapse.Dtos;
using PrintDeck.Application.Features.Timelapse.Handlers;
using PrintDeck.Application.Features.Timelapse.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Application.Timelapse;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Application.Tests.Features.Timelapse;

public class TimelapseHandlerTests
{
	private static readonly TimelapseVideo _cube = new("cube_20261119-100000.mp4", 2048, new DateTimeOffset(2026, 11, 19, 10, 5, 0, TimeSpan.Zero));
	private readonly FakeTimelapseStore _store = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public TimelapseHandlerTests()
	{
		_store.Videos.Add(_cube);
		_store.Content[_cube.Name] = [1, 2, 3];
	}

	[Fact]
	public async Task Lists_the_videos_and_what_the_recorder_does()
	{
		using TimelapseRecorder recorder = new(new FakePrinterSession(), _events.Hub, new FakeSettingsStore(), new FakeCameraSnapshots(), _store,
			TimeProvider.System, NullLogger<TimelapseRecorder>.Instance);

		TimelapsesDto dto = await new GetTimelapsesHandler(recorder, _store).Handle(new GetTimelapsesQuery(), Ct);

		Assert.Equal([_cube], dto.Videos);
		Assert.Equal(TimelapseStatus.Idle, dto.Status);
	}

	[Fact]
	public async Task Opens_a_video_by_name()
	{
		await using Stream? video = await new GetTimelapseVideoHandler(_store).Handle(new GetTimelapseVideoQuery(_cube.Name), Ct);

		Assert.NotNull(video);
		Assert.Equal(3, video.Length);
	}

	[Theory]
	[InlineData("missing.mp4")]
	[InlineData("../settings.json")]
	[InlineData(".frames")]
	public async Task Opens_nothing_else(string name)
	{
		Assert.Null(await new GetTimelapseVideoHandler(_store).Handle(new GetTimelapseVideoQuery(name), Ct));
	}

	[Fact]
	public async Task Deletes_a_video()
	{
		Result r = await new DeleteTimelapseHandler(_store, _events.Hub).Handle(new DeleteTimelapseCommand(_cube.Name), Ct);

		Assert.True(r.IsSuccess);
		Assert.Empty(_store.Videos);
		Assert.Contains(PrinterChange.Timelapse, _events.Published);
	}

	[Theory]
	[InlineData("missing.mp4")]
	[InlineData("../cube_20261119-100000.mp4")]
	public async Task Deleting_what_is_not_there_fails(string name)
	{
		Result r = await new DeleteTimelapseHandler(_store, _events.Hub).Handle(new DeleteTimelapseCommand(name), Ct);

		Assert.Equal("That timelapse doesn't exist.", r.Error);
		Assert.Single(_store.Videos);
		Assert.Empty(_events.Published);
	}
}
