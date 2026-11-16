using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Files.Enums;
using PrintDeck.Domain.Jobs;

namespace PrintDeck.Application.Tests.Features.Files;

public class FileHandlerTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
	private readonly InMemoryGcodeFileStore _files = new();
	private readonly FakePrinterSession _session = new();
	private readonly ToolpathCache _cache = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Delete(string name) =>
		new DeleteGcodeHandler(_files, _session, _events.Hub, _cache).Handle(new DeleteGcodeCommand(name), Ct);

	private ValueTask<ToolpathResult?> Toolpath(string name) =>
		new GetGcodeToolpathHandler(_files, _cache).Handle(new GetGcodeToolpathQuery(name), Ct);

	[Fact]
	public async Task List_shows_newest_uploads_first()
	{
		_files.Add("old.gcode", "G28", _t0);
		_files.Add("new.gcode", "G28", _t0.AddHours(2));
		_files.Add("mid.gcode", "G28", _t0.AddHours(1));

		IReadOnlyList<GcodeFile> list = await new ListGcodeFilesHandler(_files).Handle(new ListGcodeFilesQuery(), Ct);

		Assert.Equal(["new.gcode", "mid.gcode", "old.gcode"], list.Select(f => f.Name));
	}

	[Fact]
	public async Task List_searches_sorts_and_filters()
	{
		_files.Add("benchy.gcode", "G28", _t0);
		_files.Add("benchy-old.gcode", "G28\nG1 X1", _t0.AddHours(1));
		_files.Add("cube.gcode", "G28", _t0.AddHours(2));
		await _files.RecordPrintAsync("benchy-old.gcode", new PrintRecord(_t0, _t0.AddHours(1), JobStatus.Completed), Ct);

		IReadOnlyList<GcodeFile> list = await new ListGcodeFilesHandler(_files)
			.Handle(new ListGcodeFilesQuery("BEN", FileSort.Name, HidePrinted: true), Ct);

		Assert.Equal(["benchy.gcode"], list.Select(f => f.Name));
	}

	[Fact]
	public async Task Delete_removes_the_file_and_announces_it()
	{
		_files.Add("cube.gcode", "G28");

		Assert.True((await Delete("cube.gcode")).IsSuccess);
		Assert.False(_files.Contains("cube.gcode"));
		Assert.Contains(PrinterChange.Files, _events.Published);
	}

	[Fact]
	public async Task Delete_fails_for_a_missing_file()
	{
		Assert.Equal("File not found.", (await Delete("nope.gcode")).Error);
		Assert.Empty(_events.Published);
	}

	[Fact]
	public async Task Delete_refuses_the_file_that_is_printing()
	{
		_files.Add("cube.gcode", "G28");
		_session.CurrentJob = new PrintJob("cube.gcode", 3, null, _t0);

		Assert.Equal("Can't delete the file that is printing.", (await Delete("cube.gcode")).Error);
		Assert.True(_files.Contains("cube.gcode"));
	}

	[Fact]
	public async Task Delete_allows_a_previously_printed_file()
	{
		_files.Add("cube.gcode", "G28");
		PrintJob job = new("cube.gcode", 3, null, _t0);
		job.Complete(_t0);
		_session.CurrentJob = job;

		Assert.True((await Delete("cube.gcode")).IsSuccess);
	}

	[Fact]
	public async Task Delete_drops_the_cached_toolpath()
	{
		_files.Add("cube.gcode", "G1 X0 Y0\nG1 X10 Y0 E1\n", _t0);
		await Toolpath("cube.gcode");
		await Delete("cube.gcode");

		_files.Add("cube.gcode", "G1 X0 Y0\nG1 X20 Y0 E1\n", _t0); // same name, time and size
		ToolpathResult? again = await Toolpath("cube.gcode");

		Assert.Equal(20f, again!.Toolpath.Bounds.MaxX);
	}

	[Fact]
	public async Task Toolpath_is_null_for_a_missing_file()
	{
		Assert.Null(await Toolpath("nope.gcode"));
	}

	[Fact]
	public async Task Toolpath_returns_file_and_parsed_paths()
	{
		_files.Add("cube.gcode", "G1 X0 Y0\nG1 X10 Y0 E1\n");

		ToolpathResult? r = await Toolpath("cube.gcode");

		Assert.Equal("cube.gcode", r!.File.Name);
		Assert.Equal(2, r.Toolpath.PointCount);
	}
}
