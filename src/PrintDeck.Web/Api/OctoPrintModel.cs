using System.Globalization;

using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.SdCard.Enums;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Settings.Enums;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Web.Api;

/// <summary>
/// PrintDeck's state in the shapes OctoPrint's REST API uses (<c>/api/printer</c>, <c>/api/job</c>, <c>/api/connection</c>,
/// <c>/api/settings</c>), for clients written for OctoPrint such as Home Assistant's OctoPrint integration.
/// </summary>
public static class OctoPrintModel
{
	/// <summary>The OctoPrint version PrintDeck answers as; clients check for a recent one.</summary>
	public const string Version = "1.10.0";

	/// <summary>OctoPrint's state texts; Home Assistant maps exactly these to its status sensor.</summary>
	public static string StateText(PrinterState state, JobKind? kind = null) => state switch
	{
		PrinterState.Offline => "Offline",
		PrinterState.Connecting => "Connecting",
		PrinterState.Operational => "Operational",
		PrinterState.Printing => kind == JobKind.SdPrint ? "Printing from SD" : "Printing",
		PrinterState.Paused => "Paused",
		PrinterState.Cancelling => "Cancelling",
		PrinterState.Transferring => "Transferring file to SD",
		_ => "Error"
	};

	/// <summary>True when OctoPrint would answer <c>/api/printer</c>; otherwise it answers 409.</summary>
	public static bool IsOperational(PrinterState state) => state.IsConnected();

	public static object Flags(PrinterState state, bool sdReady) => new
	{
		operational = IsOperational(state),
		printing = state == PrinterState.Printing,
		paused = state == PrinterState.Paused,
		pausing = false,
		resuming = false,
		cancelling = state == PrinterState.Cancelling,
		finishing = false,
		ready = state == PrinterState.Operational,
		error = state == PrinterState.Error,
		closedOrError = state is PrinterState.Offline or PrinterState.Error,
		sdReady = sdReady
	};

	/// <summary>"tool0", "tool1"… for hotends, "bed" and "chamber"; Home Assistant makes one sensor of each.</summary>
	public static string HeaterName(string heater) => heater switch
	{
		"B" => "bed",
		"C" => "chamber",
		"T" => "tool0",
		_ when heater.StartsWith('T') && int.TryParse(heater.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int n) => $"tool{n}",
		_ => heater.ToLowerInvariant()
	};

	public static Dictionary<string, object> Temperatures(IReadOnlyList<HeaterReading> heaters)
	{
		Dictionary<string, object> result = new(StringComparer.Ordinal);
		foreach (HeaterReading h in heaters)
			result[HeaterName(h.Heater)] = new { actual = Math.Round(h.Actual, 2), target = Math.Round(h.Target, 2), offset = 0 };
		return result;
	}

	public static object Printer(PrinterStatusDto status, SdCardState sd) => new
	{
		temperature = Temperatures(status.Heaters),
		sd = new { ready = sd == SdCardState.Ready },
		state = new
		{
			text = StateText(status.State, status.Job?.Kind),
			flags = Flags(status.State, sd == SdCardState.Ready),
			error = status.Error ?? ""
		}
	};

	/// <param name="sizeBytes">Size of the printed file when known.</param>
	/// <param name="estimate">The slicer's estimate when known.</param>
	public static object Job(PrinterStatusDto status, long? sizeBytes, TimeSpan? estimate)
	{
		// Only a print counts as a job; an upload to the SD card is not one.
		JobDto? job = status.Job is { Kind: JobKind.Print or JobKind.SdPrint } j ? j : null;
		bool active = job?.Status is JobStatus.Printing or JobStatus.Paused;
		return new
		{
			job = new
			{
				file = new
				{
					name = job is null ? null : Path.GetFileName(job.FileName),
					path = job?.FileName,
					display = job is null ? null : Path.GetFileName(job.FileName),
					origin = job is null ? null : job.Kind == JobKind.SdPrint ? "sdcard" : "local",
					size = sizeBytes,
					date = (long?) null
				},
				estimatedPrintTime = estimate is { } e ? (double?) Math.Round(e.TotalSeconds) : null,
				lastPrintTime = (double?) null,
				filament = (object?) null,
				user = (string?) null
			},
			progress = new
			{
				completion = job is null ? (double?) null : Math.Round(job.Progress * 100, 2),
				filepos = sizeBytes is { } size && job is not null ? (long?) (long) Math.Round(size * job.Progress) : null,
				printTime = job is null ? (int?) null : (int) job.Elapsed.TotalSeconds,
				printTimeLeft = active && job!.Remaining is { } left ? (int?) (int) left.TotalSeconds : null,
				printTimeLeftOrigin = active && job!.Remaining is not null ? "estimate" : null
			},
			state = StateText(status.State, status.Job?.Kind),
			error = status.Error
		};
	}

	public static object Connection(PrinterStatusDto status, HostSettings settings, IReadOnlyList<string> ports, IReadOnlyList<int> baudRates) => new
	{
		current = new
		{
			state = StateText(status.State, status.Job?.Kind),
			port = status.State.IsConnected() ? status.Port : null,
			baudrate = status.State.IsConnected() ? (int?) status.BaudRate : null,
			printerProfile = settings.ActiveProfileId
		},
		options = new
		{
			ports,
			baudrates = baudRates,
			printerProfiles = settings.Profiles.Select(p => new { id = p.Id, name = p.Name }),
			portPreference = settings.LastPort,
			baudratePreference = settings.DefaultBaudRate,
			printerProfilePreference = settings.ActiveProfileId,
			autoconnect = settings.AutoConnect
		}
	};

	/// <summary>
	/// The part of OctoPrint's settings clients read: the webcam (Home Assistant shows the stream as a camera) and the
	/// discovery plugin's UUID, which Home Assistant uses to tell instances apart. No keys or secrets.
	/// </summary>
	public static object Settings(HostSettings settings) => new
	{
		api = new { enabled = true },
		appearance = new { name = settings.ActiveProfile.Name },
		webcam = new
		{
			webcamEnabled = !string.IsNullOrWhiteSpace(settings.WebcamStreamUrl),
			streamUrl = settings.WebcamStreamUrl ?? "",
			snapshotUrl = settings.WebcamSnapshotUrl ?? "",
			snapshotSslValidation = true,
			// OctoPrint only flips and turns 90° counter-clockwise. A half turn is both flips, and a quarter turn
			// clockwise is a quarter turn counter-clockwise plus a half turn.
			flipH = settings.WebcamFlipHorizontal ^ (settings.WebcamRotation is CameraRotation.Half or CameraRotation.Clockwise90),
			flipV = settings.WebcamFlipVertical ^ (settings.WebcamRotation is CameraRotation.Half or CameraRotation.Clockwise90),
			rotate90 = settings.WebcamRotation is CameraRotation.Clockwise90 or CameraRotation.Clockwise270,
			bitrate = "",
			streamRatio = "16:9"
		},
		plugins = new
		{
			discovery = new { upnpUuid = settings.InstanceId }
		}
	};
}
