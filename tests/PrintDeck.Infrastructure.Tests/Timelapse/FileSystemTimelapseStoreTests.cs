using Microsoft.Extensions.Options;

using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Timelapse;
using PrintDeck.Infrastructure.Processes;
using PrintDeck.Infrastructure.Storage;
using PrintDeck.Infrastructure.Tests.TestSupport;
using PrintDeck.Infrastructure.Timelapse;

namespace PrintDeck.Infrastructure.Tests.Timelapse;

public sealed class FileSystemTimelapseStoreTests : IDisposable
{
	/// <summary>Stands in for ffmpeg: writes the output file it is given (unless told not to) and answers as told.</summary>
	private sealed class FakeFfmpeg : ICommandRunner
	{
		public Func<CommandOutput?> Answer { get; set; } = () => new CommandOutput(0, "", "");
		public bool WritesVideo { get; set; } = true;
		public bool TimesOut { get; set; }
		public List<IReadOnlyList<string>> Runs { get; } = [];

		public Task<CommandOutput?> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
		{
			Assert.Equal("ffmpeg", program);
			Runs.Add(arguments);
			if (TimesOut) throw new TimeoutException("ffmpeg did not finish in time.");
			CommandOutput? answer = Answer();
			if (answer is { ExitCode: 0 } && WritesVideo) File.WriteAllBytes(arguments[^1], [0, 0, 0, 0x18, (byte) 'f', (byte) 't', (byte) 'y', (byte) 'p']);
			return Task.FromResult(answer);
		}
	}

	private readonly TempDirectory _dir = new();
	private readonly FakeFfmpeg _ffmpeg = new();
	private readonly FileSystemTimelapseStore _store;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;
	private static readonly CameraSnapshot _jpeg = new([0xFF, 0xD8, 0xFF], "image/jpeg");

	public FileSystemTimelapseStoreTests() =>
		_store = new FileSystemTimelapseStore(Options.Create(new StorageOptions { DataDirectory = _dir.Path }), _ffmpeg);

	public void Dispose() => _dir.Dispose();

	private string Root => Path.Combine(_dir.Path, "timelapse");
	private string Frames(string recording) => Path.Combine(Root, ".frames", recording);

	private async Task TwoFrames(string recording, CameraSnapshot? frame = null)
	{
		await _store.SaveFrameAsync(recording, 1, frame ?? _jpeg, Ct);
		await _store.SaveFrameAsync(recording, 2, frame ?? _jpeg, Ct);
	}

	[Fact]
	public async Task Frames_are_numbered_files_named_after_their_type()
	{
		await TwoFrames("abc");
		await _store.SaveFrameAsync("png", 1, new CameraSnapshot([0x89], "image/png"), Ct);

		Assert.Equal(["frame-00001.jpg", "frame-00002.jpg"], Directory.GetFiles(Frames("abc")).Select(Path.GetFileName).Order());
		Assert.True(File.Exists(Path.Combine(Frames("png"), "frame-00001.png")));
		Assert.Empty(_store.List()); // frames are not videos
	}

	[Fact]
	public async Task Renders_the_frames_into_the_timelapse_folder()
	{
		await TwoFrames("abc");

		Result<TimelapseVideo> r = await _store.RenderAsync("abc", "cube_20261119-100000.mp4", new TimelapseRenderOptions(30), Ct);

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal("cube_20261119-100000.mp4", r.Value!.Name);
		Assert.Equal(8, r.Value.SizeBytes);
		Assert.True(File.Exists(Path.Combine(Root, "cube_20261119-100000.mp4")));
		IReadOnlyList<string> args = Assert.Single(_ffmpeg.Runs);
		Assert.Contains(Path.Combine(Frames("abc"), "frame-%05d.jpg"), args);
		Assert.Equal([r.Value], _store.List());
	}

	[Fact]
	public async Task A_name_that_is_taken_gets_a_number()
	{
		await TwoFrames("one");
		await TwoFrames("two");

		await _store.RenderAsync("one", "cube.mp4", new TimelapseRenderOptions(25), Ct);
		Result<TimelapseVideo> second = await _store.RenderAsync("two", "cube.mp4", new TimelapseRenderOptions(25), Ct);

		Assert.Equal("cube-2.mp4", second.Value!.Name);
		Assert.Equal(["cube-2.mp4", "cube.mp4"], _store.List().Select(v => v.Name).Order());
	}

	[Fact]
	public async Task Without_ffmpeg_it_says_so()
	{
		await TwoFrames("abc");
		_ffmpeg.Answer = () => null;

		Assert.Equal(FileSystemTimelapseStore.NotInstalled, (await _store.RenderAsync("abc", "cube.mp4", new TimelapseRenderOptions(25), Ct)).Error);
		Assert.Empty(_store.List());
	}

	[Fact]
	public async Task An_ffmpeg_error_gives_its_first_line()
	{
		await TwoFrames("abc");
		_ffmpeg.Answer = () => new CommandOutput(1, "", "\nUnknown encoder 'libx264'\nmore detail\n");

		Assert.Equal("ffmpeg could not render the timelapse: Unknown encoder 'libx264'",
			(await _store.RenderAsync("abc", "cube.mp4", new TimelapseRenderOptions(25), Ct)).Error);
	}

	[Fact]
	public async Task A_render_that_takes_too_long_fails()
	{
		await TwoFrames("abc");
		_ffmpeg.TimesOut = true;

		Assert.Equal("Rendering the timelapse took too long.", (await _store.RenderAsync("abc", "cube.mp4", new TimelapseRenderOptions(25), Ct)).Error);
	}

	[Fact]
	public async Task Nothing_to_render_without_frames()
	{
		Assert.Equal("No frames were taken.", (await _store.RenderAsync("none", "cube.mp4", new TimelapseRenderOptions(25), Ct)).Error);
		Assert.Empty(_ffmpeg.Runs);
	}

	[Fact]
	public async Task Discarding_deletes_the_frames()
	{
		await TwoFrames("one");
		await TwoFrames("two");

		_store.DiscardFrames("one");
		Assert.False(Directory.Exists(Frames("one")));
		Assert.True(Directory.Exists(Frames("two")));

		_store.DiscardAllFrames();
		Assert.False(Directory.Exists(Path.Combine(Root, ".frames")));
		_store.DiscardFrames("one"); // already gone
	}

	[Fact]
	public async Task Opens_and_deletes_videos_by_name_only()
	{
		await TwoFrames("abc");
		await _store.RenderAsync("abc", "cube.mp4", new TimelapseRenderOptions(25), Ct);
		File.WriteAllText(Path.Combine(_dir.Path, "secret.mp4"), "outside");

		await using (Stream? video = _store.OpenRead("cube.mp4"))
			Assert.Equal(8, video!.Length);
		Assert.Null(_store.OpenRead("../secret.mp4"));
		Assert.Null(_store.OpenRead("missing.mp4"));
		Assert.False(_store.Delete("../secret.mp4"));
		Assert.True(File.Exists(Path.Combine(_dir.Path, "secret.mp4")));

		Assert.True(_store.Delete("cube.mp4"));
		Assert.False(_store.Delete("cube.mp4"));
		Assert.Empty(_store.List());
	}

	[Fact]
	public async Task Lists_the_newest_first()
	{
		await TwoFrames("a");
		await TwoFrames("b");
		await _store.RenderAsync("a", "old.mp4", new TimelapseRenderOptions(25), Ct);
		await _store.RenderAsync("b", "new.mp4", new TimelapseRenderOptions(25), Ct);
		File.SetLastWriteTimeUtc(Path.Combine(Root, "old.mp4"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

		Assert.Equal(["new.mp4", "old.mp4"], _store.List().Select(v => v.Name));
	}

	[Fact]
	public async Task Recording_ids_cannot_leave_the_frames_folder()
	{
		await Assert.ThrowsAsync<ArgumentException>(() => _store.SaveFrameAsync("../x", 1, _jpeg, Ct));
	}
}
