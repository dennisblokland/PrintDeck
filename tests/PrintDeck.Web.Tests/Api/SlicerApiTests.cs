using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace PrintDeck.Web.Tests.Api;

/// <summary>The OctoPrint-compatible endpoints slicers use for "send to printer".</summary>
public class SlicerApiTests(PrintDeckWebFactory app) : IClassFixture<PrintDeckWebFactory>
{
	private readonly HttpClient _client = app.CreateClient();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task<HttpRequestMessage> Authorized(HttpMethod method, string url)
	{
		HttpRequestMessage request = new(method, url);
		request.Headers.Add("X-Api-Key", await app.ApiKeyAsync());
		return request;
	}

	private async Task<HttpResponseMessage> Upload(string fileName, string content = "G28\nG1 X10\n", bool? print = null)
	{
		HttpRequestMessage request = await Authorized(HttpMethod.Post, "/api/files/local");
		MultipartFormDataContent form = new() { { new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName } };
		if (print is { } p) form.Add(new StringContent(p ? "true" : "false"), "print");
		request.Content = form;
		return await _client.SendAsync(request, Ct);
	}

	private sealed record ErrorBody(string Error);

	[Fact]
	public async Task Rejects_requests_without_an_api_key()
	{
		HttpResponseMessage r = await _client.GetAsync("/api/version", Ct);
		Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
		Assert.Equal("Invalid API key", (await r.Content.ReadFromJsonAsync<ErrorBody>(Ct))!.Error);
	}

	[Fact]
	public async Task Rejects_a_wrong_api_key()
	{
		HttpRequestMessage request = new(HttpMethod.Get, "/api/version");
		request.Headers.Add("X-Api-Key", "WRONG");
		Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request, Ct)).StatusCode);
	}

	[Fact]
	public async Task Identifies_as_octoprint()
	{
		HttpResponseMessage r = await _client.SendAsync(await Authorized(HttpMethod.Get, "/api/version"), Ct);

		r.EnsureSuccessStatusCode();
		Dictionary<string, string>? body = await r.Content.ReadFromJsonAsync<Dictionary<string, string>>(Ct);
		Assert.StartsWith("OctoPrint", body!["text"]);
	}

	[Fact]
	public async Task Accepts_the_api_key_as_query_parameter()
	{
		HttpResponseMessage r = await _client.GetAsync($"/api/version?apikey={await app.ApiKeyAsync()}", Ct);
		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
	}

	[Fact]
	public async Task Upload_stores_the_file()
	{
		HttpResponseMessage r = await Upload("../upload-test.gcode");

		Assert.Equal(HttpStatusCode.Created, r.StatusCode);
		Assert.Equal("/api/files/local/upload-test.gcode", r.Headers.Location?.OriginalString);
		Assert.NotNull((await app.Files.GetAsync("upload-test.gcode", Ct))?.Analysis);
	}

	[Fact]
	public async Task Upload_rejects_non_gcode()
	{
		HttpResponseMessage r = await Upload("model.stl");

		Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
		Assert.StartsWith("Only G-code files", (await r.Content.ReadFromJsonAsync<ErrorBody>(Ct))!.Error);
	}

	[Fact]
	public async Task Upload_needs_a_file_part()
	{
		HttpRequestMessage request = await Authorized(HttpMethod.Post, "/api/files/local");
		request.Content = new MultipartFormDataContent { { new StringContent("true"), "print" } };

		HttpResponseMessage r = await _client.SendAsync(request, Ct);

		Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
	}

	[Fact]
	public async Task Upload_and_print_conflicts_when_the_printer_is_offline()
	{
		HttpResponseMessage r = await Upload("print-me.gcode", print: true);

		Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
		Assert.Equal("Printer is not ready.", (await r.Content.ReadFromJsonAsync<ErrorBody>(Ct))!.Error);
		Assert.NotNull(await app.Files.GetAsync("print-me.gcode", Ct)); // still uploaded
	}

	[Fact]
	public async Task Upload_requires_the_api_key()
	{
		MultipartFormDataContent form = new() { { new ByteArrayContent("G28\n"u8.ToArray()), "file", "sneaky.gcode" } };

		HttpResponseMessage r = await _client.PostAsync("/api/files/local", form, Ct);

		Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
		Assert.Null(await app.Files.GetAsync("sneaky.gcode", Ct));
	}
}
