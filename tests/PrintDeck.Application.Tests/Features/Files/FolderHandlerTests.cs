using System.Text;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Jobs;

namespace PrintDeck.Application.Tests.Features.Files;

public class FolderHandlerTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
	private readonly InMemoryGcodeFileStore _files = new();
	private readonly FakePrinterSession _session = new();
	private readonly ToolpathCache _cache = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Create(string path) => new CreateFolderHandler(_files, _events.Hub).Handle(new CreateFolderCommand(path), Ct);
	private ValueTask<Result> MoveFile(string from, string to) =>
		new MoveGcodeHandler(_files, _session, _events.Hub, _cache).Handle(new MoveGcodeCommand(from, to), Ct);
	private ValueTask<Result> MoveFolder(string from, string to) =>
		new MoveFolderHandler(_files, _session, _events.Hub, _cache).Handle(new MoveFolderCommand(from, to), Ct);
	private ValueTask<Result> DeleteFolder(string path) => new DeleteFolderHandler(_files, _events.Hub).Handle(new DeleteFolderCommand(path), Ct);
	private ValueTask<Result<GcodeFile>> Upload(string name, string? folder) =>
		new UploadGcodeHandler(_files, _events.Hub).Handle(new UploadGcodeCommand(name, new MemoryStream(Encoding.UTF8.GetBytes("G28\n")), folder), Ct);
	private ValueTask<IReadOnlyList<string>> Folders() => new ListFoldersHandler(_files).Handle(new ListFoldersQuery(), Ct);

	private void Printing(string name) => _session.CurrentJob = new PrintJob(name, 3, null, _t0);

	[Fact]
	public async Task Creates_nested_folders()
	{
		Assert.True((await Create(" parts / brackets ")).IsSuccess);

		Assert.Equal(["parts", "parts/brackets"], await Folders());
		Assert.Contains(PrinterChange.Files, _events.Published);
	}

	[Theory]
	[InlineData("", "Invalid folder name.")]
	[InlineData("../outside", "Invalid folder name.")]
	[InlineData(".hidden", "Invalid folder name.")]
	public async Task Refuses_bad_folder_names(string path, string error) => Assert.Equal(error, (await Create(path)).Error);

	[Fact]
	public async Task Refuses_a_folder_that_exists()
	{
		await Create("parts");
		Assert.Equal("A folder with that name already exists.", (await Create("parts")).Error);
	}

	[Fact]
	public async Task Uploads_into_a_folder_creating_it()
	{
		Result<GcodeFile> r = await Upload("cube.gcode", "parts\\brackets");

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal("parts/brackets/cube.gcode", r.Value!.Name);
		Assert.Equal(["parts", "parts/brackets"], await Folders());
	}

	[Fact]
	public async Task Upload_refuses_a_bad_folder()
	{
		Assert.Equal("Invalid folder name.", (await Upload("cube.gcode", "../etc")).Error);
		Assert.Equal("Invalid folder name.", (await Upload("cube.gcode", ".git")).Error);
	}

	[Fact]
	public async Task Moves_and_renames_a_file_with_its_history()
	{
		_files.Add("cube.gcode", "G28");
		await _files.RecordPrintAsync("cube.gcode", new PrintRecord(_t0, _t0.AddHours(1), Domain.Jobs.Enums.JobStatus.Completed), Ct);

		Assert.True((await MoveFile("cube.gcode", "parts/cube-v2.gcode")).IsSuccess);

		Assert.False(_files.Contains("cube.gcode"));
		Assert.True(_files.Contains("parts/cube-v2.gcode"));
		Assert.Single(_files.PrintsOf("parts/cube-v2.gcode"));
		Assert.Contains(PrinterChange.Files, _events.Published);
	}

	public static TheoryData<string, string> RefusedMoves => new()
	{
		{ "missing", "File not found." },
		{ "taken", "A file with that name already exists there." },
		{ "not gcode", "Only G-code files (.gcode, .gco, .g) can be uploaded." },
		{ "bad path", "Invalid file name." },
		{ "printing", "Can't move the file that is printing." },
	};

	[Theory]
	[MemberData(nameof(RefusedMoves))]
	public async Task Refuses_file_moves(string reason, string error)
	{
		_files.Add("cube.gcode", "G28");
		_files.Add("parts/cube.gcode", "G28");
		if (reason == "printing") Printing("cube.gcode");

		Result r = reason switch
		{
			"missing" => await MoveFile("gone.gcode", "parts/gone.gcode"),
			"taken" => await MoveFile("cube.gcode", "parts/cube.gcode"),
			"not gcode" => await MoveFile("cube.gcode", "cube.txt"),
			"bad path" => await MoveFile("cube.gcode", "../cube.gcode"),
			_ => await MoveFile("cube.gcode", "parts/other.gcode")
		};

		Assert.Equal(error, r.Error);
		Assert.True(_files.Contains("cube.gcode"));
	}

	[Fact]
	public async Task Moves_a_folder_with_everything_in_it()
	{
		_files.Add("parts/cube.gcode", "G28");
		_files.Add("parts/brackets/left.gcode", "G28");

		Assert.True((await MoveFolder("parts", "archive/old parts")).IsSuccess);

		Assert.True(_files.Contains("archive/old parts/cube.gcode"));
		Assert.True(_files.Contains("archive/old parts/brackets/left.gcode"));
		Assert.Equal(["archive", "archive/old parts", "archive/old parts/brackets"], await Folders());
	}

	[Fact]
	public async Task Refuses_folder_moves_that_cannot_work()
	{
		_files.Add("parts/cube.gcode", "G28");
		_files.Add("vases/vase.gcode", "G28");

		Assert.Equal("A folder can't go inside itself.", (await MoveFolder("parts", "parts/inner")).Error);
		Assert.Equal("Folder not found.", (await MoveFolder("missing", "elsewhere")).Error);
		Assert.Equal("A folder with that name already exists.", (await MoveFolder("parts", "vases")).Error);
		Assert.Equal("Invalid folder name.", (await MoveFolder("parts", "")).Error);

		Printing("parts/cube.gcode");
		Assert.Equal("Can't move a folder while one of its files is printing.", (await MoveFolder("parts", "renamed")).Error);
		Assert.True(_files.Contains("parts/cube.gcode"));
	}

	[Fact]
	public async Task Deletes_only_empty_folders()
	{
		_files.Add("parts/cube.gcode", "G28");
		await Create("empty");
		await Create("outer/inner");

		Assert.Equal("The folder isn't empty. Move or delete what's in it first.", (await DeleteFolder("parts")).Error);
		Assert.Equal("The folder isn't empty. Move or delete what's in it first.", (await DeleteFolder("outer")).Error);
		Assert.Equal("Folder not found.", (await DeleteFolder("nope")).Error);

		Assert.True((await DeleteFolder("empty")).IsSuccess);
		Assert.DoesNotContain("empty", await Folders());
	}

	[Fact]
	public async Task Lists_only_one_folder_when_asked()
	{
		_files.Add("top.gcode", "G28");
		_files.Add("parts/cube.gcode", "G28");

		IReadOnlyList<GcodeFile> top = await new ListGcodeFilesHandler(_files).Handle(new ListGcodeFilesQuery(Folder: ""), Ct);
		IReadOnlyList<GcodeFile> all = await new ListGcodeFilesHandler(_files).Handle(new ListGcodeFilesQuery(), Ct);

		Assert.Equal(["top.gcode"], top.Select(f => f.Name));
		Assert.Equal(2, all.Count);
	}
}
