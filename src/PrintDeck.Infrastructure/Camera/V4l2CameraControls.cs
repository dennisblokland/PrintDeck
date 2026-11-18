using System.Text.RegularExpressions;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Infrastructure.Virtual;
using PrintDeck.Infrastructure.Processes;

namespace PrintDeck.Infrastructure.Camera;

/// <summary>
/// Camera controls through <c>v4l2-ctl</c> (Debian/Raspberry Pi OS package <c>v4l-utils</c>). The device
/// <see cref="VirtualV4l2Ctl.DeviceName"/> goes to the in-process virtual camera instead.
/// </summary>
public sealed partial class V4l2CameraControls(ICommandRunner host, ICommandRunner virtualCamera, Func<IEnumerable<string>> hostDevices)
	: ICameraControls
{
	private const string Program = "v4l2-ctl";
	private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

	[GeneratedRegex(@"^video\d$")]
	private static partial Regex CaptureNode();

	/// <summary>
	/// Stable names under /dev/v4l/by-id first (they survive replugging), then /dev/video0-9. Higher /dev/video
	/// numbers are usually the Raspberry Pi's codec and ISP nodes, which have no picture controls.
	/// </summary>
	public static IEnumerable<string> HostDevices()
	{
		const string byId = "/dev/v4l/by-id";
		IEnumerable<string> stable = Directory.Exists(byId) ? Directory.EnumerateFileSystemEntries(byId).Order() : [];
		IEnumerable<string> nodes = Directory.Exists("/dev")
			? Directory.EnumerateFileSystemEntries("/dev", "video*").Where(p => CaptureNode().IsMatch(Path.GetFileName(p))).Order()
			: [];
		return stable.Concat(nodes);
	}

	public IReadOnlyList<string> Devices() => [.. hostDevices(), VirtualV4l2Ctl.DeviceName];

	public async Task<Result<IReadOnlyList<CameraControl>>> ListAsync(string device, CancellationToken ct)
	{
		Result<string> output = await RunAsync(device, ["--list-ctrls-menus"], ct);
		return output.IsSuccess
			? Result.Success(V4l2ControlList.Parse(output.Value!))
			: Result.Failure<IReadOnlyList<CameraControl>>(output.Error!);
	}

	public async Task<Result> SetAsync(string device, IReadOnlyList<CameraControlValue> values, CancellationToken ct)
	{
		foreach (CameraControlValue v in values)
		{
			if (!V4l2ControlList.IsValidName(v.Name)) return Result.Failure($"'{v.Name}' is not a camera control.");
			Result<string> output = await RunAsync(device, [$"--set-ctrl={v.Name}={v.Value}"], ct);
			if (!output.IsSuccess) return Result.Failure(output.Error!);
		}
		return Result.Success();
	}

	private async Task<Result<string>> RunAsync(string device, string[] arguments, CancellationToken ct)
	{
		if (!Devices().Contains(device)) return Result.Failure<string>("That camera is not connected.");

		ICommandRunner runner = device == VirtualV4l2Ctl.DeviceName ? virtualCamera : host;
		CommandOutput? output;
		try
		{
			output = await runner.RunAsync(Program, ["-d", device, .. arguments], _timeout, ct);
		}
		catch (TimeoutException)
		{
			return Result.Failure<string>("The camera did not answer.");
		}

		if (output is null) return Result.Failure<string>("v4l2-ctl is not installed. Install v4l-utils on the host.");
		if (output.ExitCode == 0) return Result.Success(output.Output);

		string? reason = output.Error.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
		return Result.Failure<string>(reason is null ? $"v4l2-ctl failed (exit code {output.ExitCode})." : $"The camera refused: {reason}");
	}
}
