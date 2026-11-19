using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeTimelapseStore : ITimelapseStore
{
	private readonly Lock _lock = new();
	private readonly Dictionary<string, List<int>> _frames = [];

	public List<(string Recording, string Name, TimelapseRenderOptions Options, int Frames)> Rendered { get; } = [];
	public List<string> Discarded { get; } = [];
	public bool DiscardedAll { get; private set; }
	public Result<TimelapseVideo>? RenderResult { get; set; }
	public List<TimelapseVideo> Videos { get; } = [];
	public Dictionary<string, byte[]> Content { get; } = [];

	/// <summary>Frames saved so far, over every recording.</summary>
	public int FrameCount
	{
		get { lock (_lock) return _frames.Values.Sum(f => f.Count); }
	}

	public Task SaveFrameAsync(string recording, int number, CameraSnapshot frame, CancellationToken ct)
	{
		lock (_lock)
		{
			if (!_frames.TryGetValue(recording, out List<int>? frames)) _frames[recording] = frames = [];
			Assert.Equal(frames.Count + 1, number); // numbered from 1, no gaps
			frames.Add(number);
		}
		return Task.CompletedTask;
	}

	public Task<Result<TimelapseVideo>> RenderAsync(string recording, string videoName, TimelapseRenderOptions options, CancellationToken ct)
	{
		lock (_lock) Rendered.Add((recording, videoName, options, _frames.GetValueOrDefault(recording)?.Count ?? 0));
		return Task.FromResult(RenderResult ?? Result.Success(new TimelapseVideo(videoName, 1000, DateTimeOffset.UnixEpoch)));
	}

	public void DiscardFrames(string recording)
	{
		lock (_lock)
		{
			Discarded.Add(recording);
			_frames.Remove(recording);
		}
	}

	public void DiscardAllFrames() => DiscardedAll = true;

	public IReadOnlyList<TimelapseVideo> List() => Videos;

	public Stream? OpenRead(string name) => Content.TryGetValue(name, out byte[]? bytes) ? new MemoryStream(bytes) : null;

	public bool Delete(string name)
	{
		Content.Remove(name);
		return Videos.RemoveAll(v => v.Name == name) > 0;
	}
}
