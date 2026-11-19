using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Web.Tests.Api;

/// <summary>The OctoPrint status and control endpoints against the real app and the virtual printer.</summary>
public sealed class OctoPrintApiTests : IAsyncDisposable
{
	private readonly PrintDeckWebFactory _app = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ValueTask DisposeAsync() => _app.DisposeAsync();

	private async Task<string> KeyFor(string userName, UserRole role)
	{
		await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<IUserAccounts>().CreateAsync(userName, "password123", role, Ct);
		return (await scope.ServiceProvider.GetRequiredService<IApiKeys>().CreateAsync(userName, "Home Assistant", Ct))!;
	}

	private async Task<HttpResponseMessage> Send(HttpMethod method, string url, string key, object? body = null)
	{
		HttpRequestMessage request = new(method, url);
		request.Headers.Add("X-Api-Key", key);
		if (body is not null) request.Content = JsonContent.Create(body);
		return await _app.CreateClient().SendAsync(request, Ct);
	}

	private async Task<JsonElement> Json(string url, string key)
	{
		HttpResponseMessage r = await Send(HttpMethod.Get, url, key);
		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
		return await r.Content.ReadFromJsonAsync<JsonElement>(Ct);
	}

	private async Task Until(Func<Task<bool>> condition)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(15);
		while (!await condition())
		{
			if (DateTime.UtcNow > deadline) throw new TimeoutException();
			await Task.Delay(100, Ct);
		}
	}

	[Fact]
	public async Task Needs_a_key()
	{
		Assert.Equal(HttpStatusCode.Forbidden, (await _app.CreateClient().GetAsync("/api/job", Ct)).StatusCode);
	}

	[Fact]
	public async Task Offline_the_printer_answers_409_and_the_job_says_offline()
	{
		string key = await KeyFor("ada", UserRole.Admin);

		Assert.Equal(HttpStatusCode.Conflict, (await Send(HttpMethod.Get, "/api/printer", key)).StatusCode);
		Assert.Equal("Offline", (await Json("/api/job", key)).GetProperty("state").GetString());
		Assert.Equal("Offline", (await Json("/api/connection", key)).GetProperty("current").GetProperty("state").GetString());
	}

	[Fact]
	public async Task Connects_reports_temperatures_and_disconnects()
	{
		string key = await KeyFor("ada", UserRole.Admin);

		HttpResponseMessage connect = await Send(HttpMethod.Post, "/api/connection", key, new { command = "connect", port = "VIRTUAL", baudrate = 115200 });
		Assert.Equal(HttpStatusCode.NoContent, connect.StatusCode);

		await Until(async () => (await Send(HttpMethod.Get, "/api/printer", key)).StatusCode == HttpStatusCode.OK);
		await Until(async () => (await Json("/api/printer", key)).GetProperty("temperature").TryGetProperty("tool0", out _));
		JsonElement printer = await Json("/api/printer", key);
		Assert.True(printer.GetProperty("temperature").TryGetProperty("bed", out _));
		Assert.Equal("Operational", printer.GetProperty("state").GetProperty("text").GetString());
		Assert.True(printer.GetProperty("state").GetProperty("flags").GetProperty("ready").GetBoolean());

		JsonElement connection = await Json("/api/connection", key);
		Assert.Equal("VIRTUAL", connection.GetProperty("current").GetProperty("port").GetString());
		Assert.Contains("VIRTUAL", connection.GetProperty("options").GetProperty("ports").EnumerateArray().Select(p => p.GetString()));

		Assert.Equal(HttpStatusCode.NoContent, (await Send(HttpMethod.Post, "/api/connection", key, new { command = "disconnect" })).StatusCode);
		await Until(async () => (await Send(HttpMethod.Get, "/api/printer", key)).StatusCode == HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task Pauses_resumes_and_cancels_a_print()
	{
		string key = await KeyFor("ada", UserRole.Admin);
		await Send(HttpMethod.Post, "/api/connection", key, new { command = "connect", port = "VIRTUAL", baudrate = 115200 });
		await Until(async () => (await Json("/api/connection", key)).GetProperty("current").GetProperty("state").GetString() == "Operational");
		string gcode = "G28\n" + string.Concat(Enumerable.Range(0, 2000).Select(i => $"G1 X{i % 200} Y{i % 200} F600\n"));
		HttpRequestMessage upload = new(HttpMethod.Post, "/api/files/local")
		{
			Content = new MultipartFormDataContent
			{
				{ new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(gcode)), "file", "tower.gcode" },
				{ new StringContent("true"), "print" }
			}
		};
		upload.Headers.Add("X-Api-Key", key);
		Assert.Equal(HttpStatusCode.Created, (await _app.CreateClient().SendAsync(upload, Ct)).StatusCode);

		JsonElement job = await Json("/api/job", key);
		Assert.Equal("tower.gcode", job.GetProperty("job").GetProperty("file").GetProperty("name").GetString());
		Assert.Equal(gcode.Length, job.GetProperty("job").GetProperty("file").GetProperty("size").GetInt32());

		Assert.Equal(HttpStatusCode.NoContent, (await Send(HttpMethod.Post, "/api/job", key, new { command = "pause", action = "pause" })).StatusCode);
		await Until(async () => (await Json("/api/job", key)).GetProperty("state").GetString() == "Paused");
		Assert.Equal(HttpStatusCode.NoContent, (await Send(HttpMethod.Post, "/api/job", key, new { command = "pause", action = "toggle" })).StatusCode);
		await Until(async () => (await Json("/api/job", key)).GetProperty("state").GetString() == "Printing");
		Assert.Equal(HttpStatusCode.NoContent, (await Send(HttpMethod.Post, "/api/job", key, new { command = "cancel" })).StatusCode);
		await Until(async () => (await Json("/api/job", key)).GetProperty("state").GetString() == "Operational");

		// Nothing to pause any more: OctoPrint answers 409.
		Assert.Equal(HttpStatusCode.Conflict, (await Send(HttpMethod.Post, "/api/job", key, new { command = "pause", action = "pause" })).StatusCode);
	}

	[Fact]
	public async Task Start_needs_a_selected_file_which_printdeck_does_not_have()
	{
		string key = await KeyFor("ada", UserRole.Admin);

		HttpResponseMessage r = await Send(HttpMethod.Post, "/api/job", key, new { command = "start" });

		Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
		Assert.Equal(PrintDeck.Web.Api.OctoPrintApi.NoSelectedFile, (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("error").GetString());
		Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Post, "/api/job", key, new { command = "jump" })).StatusCode);
	}

	[Fact]
	public async Task A_viewers_key_reads_but_does_not_control()
	{
		string key = await KeyFor("vic", UserRole.Viewer);

		Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Get, "/api/job", key)).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/api/connection", key, new { command = "connect", port = "VIRTUAL" })).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/api/job", key, new { command = "cancel" })).StatusCode);
	}

	[Fact]
	public async Task Settings_give_a_stable_id_and_no_secrets()
	{
		string key = await KeyFor("ada", UserRole.Admin);

		JsonElement first = await Json("/api/settings", key);
		JsonElement second = await Json("/api/settings", key);

		string? id = first.GetProperty("plugins").GetProperty("discovery").GetProperty("upnpUuid").GetString();
		Assert.True(Guid.TryParse(id, out _));
		Assert.Equal(id, second.GetProperty("plugins").GetProperty("discovery").GetProperty("upnpUuid").GetString());
		Assert.DoesNotContain(await _app.ApiKeyAsync(), first.GetRawText());
		Assert.Equal("1.10.0", (await Json("/api/server", key)).GetProperty("version").GetString());
	}
}
