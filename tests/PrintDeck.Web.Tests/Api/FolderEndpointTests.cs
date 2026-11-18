using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using PrintDeck.Application.Files.Thumbnails;

namespace PrintDeck.Web.Tests.Api;

/// <summary>Folders through the slicer API ("path", "foldername") and file URLs with folders in them.</summary>
public class FolderEndpointTests(PrintDeckWebFactory app) : IClassFixture<PrintDeckWebFactory>
{
	private readonly HttpClient _client = app.CreateClient();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task<HttpResponseMessage> Post(MultipartFormDataContent form)
	{
		HttpRequestMessage request = new(HttpMethod.Post, "/api/files/local") { Content = form };
		request.Headers.Add("X-Api-Key", await app.ApiKeyAsync());
		return await _client.SendAsync(request, Ct);
	}

	private async Task<HttpResponseMessage> Get(string url)
	{
		HttpRequestMessage request = new(HttpMethod.Get, url);
		request.Headers.Add("X-Api-Key", await app.ApiKeyAsync());
		return await _client.SendAsync(request, Ct);
	}

	private static MultipartFormDataContent File(string name, string content, string? path = null)
	{
		MultipartFormDataContent form = new() { { new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", name } };
		if (path is not null) form.Add(new StringContent(path), "path");
		return form;
	}

	[Fact]
	public async Task Uploads_into_the_path_folder()
	{
		HttpResponseMessage r = await Post(File("cube.gcode", "G28\n", "slicer/parts"));

		Assert.Equal(HttpStatusCode.Created, r.StatusCode);
		Assert.Equal("/api/files/local/slicer/parts/cube.gcode", r.Headers.Location?.OriginalString);
		JsonElement local = (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("files").GetProperty("local");
		Assert.Equal(("cube.gcode", "slicer/parts/cube.gcode"), (local.GetProperty("name").GetString(), local.GetProperty("path").GetString()));
		Assert.NotNull(await app.Files.GetAsync("slicer/parts/cube.gcode", Ct));
	}

	[Fact]
	public async Task Refuses_a_path_outside_the_uploads()
	{
		HttpResponseMessage r = await Post(File("cube.gcode", "G28\n", "../escape"));

		Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
		Assert.Contains("Invalid folder name.", await r.Content.ReadAsStringAsync(Ct));
	}

	[Fact]
	public async Task Creates_a_folder_from_foldername()
	{
		MultipartFormDataContent form = new() { { new StringContent("new one"), "foldername" }, { new StringContent("made"), "path" } };

		HttpResponseMessage r = await Post(form);

		Assert.Equal(HttpStatusCode.Created, r.StatusCode);
		Assert.Equal("made/new one", (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("folder").GetProperty("path").GetString());
		Assert.Contains("made/new one", app.Files.ListFolders());

		Assert.Equal(HttpStatusCode.Conflict, (await Post(new MultipartFormDataContent { { new StringContent("new one"), "foldername" }, { new StringContent("made"), "path" } })).StatusCode);
	}

	[Fact]
	public async Task Serves_thumbnails_and_toolpaths_of_files_in_folders()
	{
		byte[] png = PngEncoder.Encode(new RgbaImage(2, 1, [255, 0, 0, 255, 0, 0, 255, 255]));
		string base64 = Convert.ToBase64String(png);
		string gcode = $"; thumbnail begin 2x1 {base64.Length}\n; {base64}\n; thumbnail end\nG1 X0 Y0\nG1 X10 Y0 E1\n";
		Assert.Equal(HttpStatusCode.Created, (await Post(File("my part.gcode", gcode, "deep/er"))).StatusCode);

		HttpResponseMessage thumbnail = await Get("/thumbnail/deep/er/my%20part.gcode");
		HttpResponseMessage toolpath = await Get("/toolpath/deep/er/my%20part.gcode");

		Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
		Assert.Equal(png, await thumbnail.Content.ReadAsByteArrayAsync(Ct));
		Assert.Equal(HttpStatusCode.OK, toolpath.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await Get("/thumbnail/deep/../er/my%20part.gcode")).StatusCode);
	}
}
