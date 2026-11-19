using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Timelapse;
using PrintDeck.Infrastructure.Processes;
using PrintDeck.Infrastructure.Storage;

namespace PrintDeck.Infrastructure.Timelapse;

/// <summary>
/// Videos in the timelapse folder; the frames of a recording in <c>.frames/&lt;recording&gt;/</c> inside it until they are
/// rendered with ffmpeg (which must be installed on the host).
/// </summary>
public sealed class FileSystemTimelapseStore(IOptions<StorageOptions> options, ICommandRunner commands) : ITimelapseStore
{
	public const string NotInstalled = "ffmpeg is not installed, so the timelapse could not be rendered.";
	public static readonly TimeSpan RenderTimeout = TimeSpan.FromHours(1);

	private readonly string _root = Directory.CreateDirectory(options.Value.TimelapseDirectory).FullName;

	private string FramesRoot => Path.Combine(_root, ".frames");

	private string FramesOf(string recording)
	{
		if (recording.Length == 0 || !recording.All(char.IsAsciiLetterOrDigit))
			throw new ArgumentException("Invalid recording id.", nameof(recording));
		return Path.Combine(FramesRoot, recording);
	}

	private string? VideoPath(string name) => TimelapseName.IsValid(name) ? Path.Combine(_root, name) : null;

	public async Task SaveFrameAsync(string recording, int number, CameraSnapshot frame, CancellationToken ct)
	{
		string folder = Directory.CreateDirectory(FramesOf(recording)).FullName;
		await File.WriteAllBytesAsync(Path.Combine(folder, FfmpegTimelapse.FrameName(number, Extension(frame.ContentType))), frame.Content, ct);
	}

	public async Task<Result<TimelapseVideo>> RenderAsync(string recording, string videoName, TimelapseRenderOptions render, CancellationToken ct)
	{
		string folder = FramesOf(recording);
		string? first = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "frame-00001.*").FirstOrDefault() : null;
		if (first is null) return Result.Failure<TimelapseVideo>("No frames were taken.");

		string output = Path.Combine(folder, "video.mp4");
		CommandOutput? result;
		try
		{
			result = await commands.RunAsync(FfmpegTimelapse.Program,
				FfmpegTimelapse.Arguments(folder, Path.GetExtension(first), output, render), RenderTimeout, ct);
		}
		catch (TimeoutException)
		{
			return Result.Failure<TimelapseVideo>("Rendering the timelapse took too long.");
		}
		if (result is null) return Result.Failure<TimelapseVideo>(NotInstalled);
		if (result.ExitCode != 0 || !File.Exists(output))
		{
			string reason = result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? $"exit code {result.ExitCode}";
			return Result.Failure<TimelapseVideo>($"ffmpeg could not render the timelapse: {reason}");
		}

		string target = FreeName(videoName);
		File.Move(output, target);
		FileInfo info = new(target);
		return Result.Success(new TimelapseVideo(info.Name, info.Length, info.LastWriteTimeUtc));
	}

	/// <summary>The name, or name-2, name-3… when a video of that name exists (two prints started in the same second).</summary>
	private string FreeName(string videoName)
	{
		string name = TimelapseName.IsValid(videoName) ? videoName : "timelapse" + TimelapseName.Extension;
		string stem = Path.GetFileNameWithoutExtension(name);
		string path = Path.Combine(_root, name);
		for (int n = 2; File.Exists(path); n++) path = Path.Combine(_root, $"{stem}-{n}{TimelapseName.Extension}");
		return path;
	}

	public void DiscardFrames(string recording)
	{
		string folder = FramesOf(recording);
		if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
	}

	public void DiscardAllFrames()
	{
		if (Directory.Exists(FramesRoot)) Directory.Delete(FramesRoot, recursive: true);
	}

	public IReadOnlyList<TimelapseVideo> List() =>
		new DirectoryInfo(_root).EnumerateFiles("*" + TimelapseName.Extension)
			.Where(f => TimelapseName.IsValid(f.Name))
			.Select(f => new TimelapseVideo(f.Name, f.Length, f.LastWriteTimeUtc))
			.OrderByDescending(v => v.CreatedAt)
			.ThenBy(v => v.Name, StringComparer.Ordinal)
			.ToList();

	public Stream? OpenRead(string name)
	{
		string? path = VideoPath(name);
		if (path is null || !File.Exists(path)) return null;
		try
		{
			return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 64 * 1024, FileOptions.Asynchronous);
		}
		catch (FileNotFoundException)
		{
			return null;
		}
	}

	public bool Delete(string name)
	{
		string? path = VideoPath(name);
		if (path is null || !File.Exists(path)) return false;
		File.Delete(path);
		return true;
	}

	private static string Extension(string contentType) => contentType switch
	{
		"image/png" => ".png",
		"image/webp" => ".webp",
		_ => ".jpg"
	};
}
