using System.Text.Json;

using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.SdCard.Enums;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Settings.Enums;
using PrintDeck.Domain.Temperatures;
using PrintDeck.Web.Api;

namespace PrintDeck.Web.Tests.Api;

public class OctoPrintModelTests
{
	private static JsonElement Json(object value) =>
		JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

	private static PrinterStatusDto Status(PrinterState state, JobDto? job = null) =>
		new(state, "/dev/ttyACM0", 115200, "Prusa-Firmware 3.14.1", null, job, [new("T0", 215.04, 215), new("B", 60.2, 60)]);

	private static readonly JobDto _printing = new("parts/benchy.gcode", JobStatus.Printing, 0.4251, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30), 1234, DateTimeOffset.UnixEpoch);

	[Theory]
	[InlineData(PrinterState.Offline, null, "Offline")]
	[InlineData(PrinterState.Connecting, null, "Connecting")]
	[InlineData(PrinterState.Operational, null, "Operational")]
	[InlineData(PrinterState.Printing, JobKind.Print, "Printing")]
	[InlineData(PrinterState.Printing, JobKind.SdPrint, "Printing from SD")]
	[InlineData(PrinterState.Paused, JobKind.Print, "Paused")]
	[InlineData(PrinterState.Cancelling, JobKind.Print, "Cancelling")]
	[InlineData(PrinterState.Transferring, JobKind.SdUpload, "Transferring file to SD")]
	[InlineData(PrinterState.Error, null, "Error")]
	public void State_texts_are_the_ones_home_assistant_knows(PrinterState state, JobKind? kind, string text)
	{
		Assert.Equal(text, OctoPrintModel.StateText(state, kind));
	}

	[Theory]
	[InlineData("T0", "tool0")]
	[InlineData("T", "tool0")]
	[InlineData("T1", "tool1")]
	[InlineData("B", "bed")]
	[InlineData("C", "chamber")]
	public void Heaters_get_octoprint_names(string heater, string name) => Assert.Equal(name, OctoPrintModel.HeaterName(heater));

	[Fact]
	public void Printer_has_temperatures_and_state_flags()
	{
		JsonElement json = Json(OctoPrintModel.Printer(Status(PrinterState.Printing, _printing), SdCardState.Ready));

		Assert.Equal(215.04, json.GetProperty("temperature").GetProperty("tool0").GetProperty("actual").GetDouble());
		Assert.Equal(60, json.GetProperty("temperature").GetProperty("bed").GetProperty("target").GetDouble());
		Assert.Equal("Printing", json.GetProperty("state").GetProperty("text").GetString());
		JsonElement flags = json.GetProperty("state").GetProperty("flags");
		Assert.True(flags.GetProperty("printing").GetBoolean());
		Assert.True(flags.GetProperty("operational").GetBoolean());
		Assert.False(flags.GetProperty("ready").GetBoolean());
		Assert.False(flags.GetProperty("closedOrError").GetBoolean());
		Assert.True(flags.GetProperty("sdReady").GetBoolean());
	}

	[Fact]
	public void Job_has_the_file_progress_and_times()
	{
		JsonElement json = Json(OctoPrintModel.Job(Status(PrinterState.Printing, _printing), 1000, TimeSpan.FromMinutes(55)));

		JsonElement file = json.GetProperty("job").GetProperty("file");
		Assert.Equal(("benchy.gcode", "parts/benchy.gcode", "local", 1000), (file.GetProperty("name").GetString(), file.GetProperty("path").GetString(),
			file.GetProperty("origin").GetString(), file.GetProperty("size").GetInt32()));
		Assert.Equal(3300, json.GetProperty("job").GetProperty("estimatedPrintTime").GetDouble());
		JsonElement progress = json.GetProperty("progress");
		Assert.Equal(42.51, progress.GetProperty("completion").GetDouble());
		Assert.Equal(425, progress.GetProperty("filepos").GetInt64());
		Assert.Equal(1200, progress.GetProperty("printTime").GetInt32());
		Assert.Equal(1800, progress.GetProperty("printTimeLeft").GetInt32());
		Assert.Equal("Printing", json.GetProperty("state").GetString());
	}

	[Fact]
	public void Without_a_print_the_job_is_empty_but_shaped()
	{
		JsonElement json = Json(OctoPrintModel.Job(Status(PrinterState.Operational), null, null));

		Assert.Equal(JsonValueKind.Null, json.GetProperty("job").GetProperty("file").GetProperty("name").ValueKind);
		Assert.Equal(JsonValueKind.Null, json.GetProperty("progress").GetProperty("completion").ValueKind);
		Assert.Equal("Operational", json.GetProperty("state").GetString());
	}

	[Fact]
	public void An_sd_card_upload_is_not_a_job()
	{
		JobDto upload = _printing with { Kind = JobKind.SdUpload };
		JsonElement json = Json(OctoPrintModel.Job(Status(PrinterState.Transferring, upload), null, null));

		Assert.Equal(JsonValueKind.Null, json.GetProperty("job").GetProperty("file").GetProperty("name").ValueKind);
		Assert.Equal("Transferring file to SD", json.GetProperty("state").GetString());
	}

	[Theory]
	[InlineData(CameraRotation.None, false, false, false)]
	[InlineData(CameraRotation.Half, true, true, false)]
	[InlineData(CameraRotation.Clockwise270, false, false, true)]
	[InlineData(CameraRotation.Clockwise90, true, true, true)]
	public void Settings_carry_the_camera_turned_octoprints_way(CameraRotation rotation, bool flipH, bool flipV, bool rotate90)
	{
		HostSettings s = new() { WebcamStreamUrl = "http://cam/stream", WebcamRotation = rotation, InstanceId = "6f1c", ApiKey = "SECRET" };

		JsonElement json = Json(OctoPrintModel.Settings(s));

		JsonElement webcam = json.GetProperty("webcam");
		Assert.True(webcam.GetProperty("webcamEnabled").GetBoolean());
		Assert.Equal("http://cam/stream", webcam.GetProperty("streamUrl").GetString());
		Assert.Equal((flipH, flipV, rotate90), (webcam.GetProperty("flipH").GetBoolean(), webcam.GetProperty("flipV").GetBoolean(), webcam.GetProperty("rotate90").GetBoolean()));
		Assert.Equal("6f1c", json.GetProperty("plugins").GetProperty("discovery").GetProperty("upnpUuid").GetString());
		Assert.DoesNotContain("SECRET", json.GetRawText());
	}

	[Fact]
	public void Without_a_stream_the_camera_is_off()
	{
		Assert.False(Json(OctoPrintModel.Settings(new HostSettings())).GetProperty("webcam").GetProperty("webcamEnabled").GetBoolean());
	}
}
