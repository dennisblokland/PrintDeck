using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Application.Timelapse;

/// <summary>One print's timelapse while its frames are taken.</summary>
internal sealed class TimelapseRecording(PrintJob job, Uri camera, TimelapseSchedule schedule, TimelapseRenderOptions render)
{
	public string Id { get; } = Guid.NewGuid().ToString("N");
	public PrintJob Job => job;
	public Uri Camera => camera;
	public TimelapseSchedule Schedule => schedule;
	public TimelapseRenderOptions Render => render;
	public CancellationTokenSource Stop { get; } = new();

	/// <summary>Frames saved; frame numbers run 1..Frames without gaps.</summary>
	public int Frames;

	/// <summary>1 while a frame is being taken; a frame that comes due meanwhile is skipped.</summary>
	public int Capturing;

	public Task Loop = Task.CompletedTask;
	public Task LastCapture = Task.CompletedTask;
}
