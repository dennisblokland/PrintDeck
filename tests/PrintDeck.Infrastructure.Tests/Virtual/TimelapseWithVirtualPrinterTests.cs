using System.Text;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Timelapse;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Timelapse;
using PrintDeck.Domain.Timelapse.Enums;
using PrintDeck.Infrastructure.Camera;
using PrintDeck.Infrastructure.Processes;
using PrintDeck.Infrastructure.Serial;
using PrintDeck.Infrastructure.Storage;
using PrintDeck.Infrastructure.Tests.TestSupport;
using PrintDeck.Infrastructure.Timelapse;
using PrintDeck.Infrastructure.Virtual;

namespace PrintDeck.Infrastructure.Tests.Virtual;

/// <summary>End-to-end: a per-layer timelapse of a print on the virtual printer, from the virtual camera, into the real store.</summary>
public sealed class TimelapseWithVirtualPrinterTests : IAsyncLifetime
{
	/// <summary>Stands in for ffmpeg: counts the frames it would encode and writes a video file.</summary>
	private sealed class FrameCountingFfmpeg : ICommandRunner
	{
		public string[] Frames { get; private set; } = [];

		public Task<CommandOutput?> RunAsync(string program, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
		{
			string pattern = arguments[arguments.ToList().IndexOf("-i") + 1];
			Frames = Directory.GetFiles(Path.GetDirectoryName(pattern)!, "frame-*").Order().ToArray();
			File.WriteAllBytes(arguments[^1], [1, 2, 3]);
			return Task.FromResult<CommandOutput?>(new CommandOutput(0, "", ""));
		}
	}

	private readonly TempDirectory _dir = new();
	private readonly PrinterEventHub _events = new();
	private readonly FrameCountingFfmpeg _ffmpeg = new();
	private readonly PrinterSession _session;
	private readonly FileSystemTimelapseStore _store;
	private readonly TimelapseRecorder _recorder;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public TimelapseWithVirtualPrinterTests()
	{
		IOptions<StorageOptions> storage = Options.Create(new StorageOptions { DataDirectory = _dir.Path });
		JsonSettingsStore settings = new(storage);
		settings.UpdateAsync(s => s with
		{
			WebcamSnapshotUrl = SnapshotSource.Virtual,
			Timelapse = new TimelapseSettings(TimelapseMode.Layer, 10, 25)
		}, CancellationToken.None).GetAwaiter().GetResult();

		_session = new PrinterSession(new TransportFactory(TimeProvider.System), _events, TimeProvider.System, NullLogger<PrinterSession>.Instance);
		_store = new FileSystemTimelapseStore(storage, _ffmpeg);
		_recorder = new TimelapseRecorder(_session, _events, settings,
			new CameraSnapshots(new HttpCameraSnapshots(), new VirtualCamera(TimeProvider.System)), _store,
			TimeProvider.System, NullLogger<TimelapseRecorder>.Instance);
		_recorder.Start();
	}

	public async ValueTask InitializeAsync()
	{
		Result result = await _session.ConnectAsync(VirtualPrinterTransport.PortName, 115200, Ct);
		Assert.True(result.IsSuccess, result.Error);
	}

	public async ValueTask DisposeAsync()
	{
		_recorder.Dispose();
		await _session.DisposeAsync();
		_dir.Dispose();
	}

	[Fact]
	public async Task Takes_a_frame_per_layer_and_renders_them_when_the_print_is_done()
	{
		StringBuilder gcode = new("G28\nG90\n");
		for (int layer = 0; layer < 5; layer++)
		{
			gcode.Append(";LAYER_CHANGE\n").Append($"G1 Z{0.2 * (layer + 1):0.0} F600\n");
			for (int i = 0; i < 10; i++) gcode.Append($"G1 X{i * 5} Y{i * 5} F12000\n");
		}
		byte[] bytes = Encoding.UTF8.GetBytes(gcode.ToString());

		Assert.True(_session.StartJob("tower.gcode", new MemoryStream(bytes), bytes.Length, null).IsSuccess);
		DateTime deadline = DateTime.UtcNow.AddSeconds(20);
		while (_session.CurrentJob!.Status != JobStatus.Completed && DateTime.UtcNow < deadline) await Task.Delay(50, Ct);
		Assert.Equal(JobStatus.Completed, _session.CurrentJob.Status);
		await _recorder.Pending;

		TimelapseVideo video = Assert.Single(_store.List());
		Assert.StartsWith("tower_", video.Name);
		// A layer that starts while the last frame is still being taken gets none, so up to 5 layer frames, plus the finished print.
		Assert.InRange(_ffmpeg.Frames.Length, TimelapseRecorder.MinFrames, 6);
		Assert.All(_ffmpeg.Frames, f => Assert.EndsWith(".png", f));
		Assert.Empty(Directory.GetDirectories(Path.Combine(_dir.Path, "timelapse", ".frames"))); // frames are gone
		Assert.Null(_recorder.Status.LastError);
	}
}
