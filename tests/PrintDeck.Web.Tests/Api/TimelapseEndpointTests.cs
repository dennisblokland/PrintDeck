using System.Net;
using System.Net.Http.Headers;

namespace PrintDeck.Web.Tests.Api;

public sealed class TimelapseEndpointTests : IAsyncDisposable
{
	private readonly PrintDeckWebFactory _app = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;
	private static readonly byte[] _video = [.. Enumerable.Range(0, 100).Select(i => (byte) i)];

	public async ValueTask DisposeAsync() => await _app.DisposeAsync();

	private void AddVideo(string name)
	{
		_ = _app.Services; // starts the app, which creates the folder
		string folder = Path.Combine(_app.DataDirectory, "timelapse");
		Directory.CreateDirectory(folder);
		File.WriteAllBytes(Path.Combine(folder, name), _video);
	}

	private async Task<HttpResponseMessage> Get(string url, RangeHeaderValue? range = null)
	{
		HttpRequestMessage request = new(HttpMethod.Get, url);
		request.Headers.Add("X-Api-Key", await _app.ApiKeyAsync());
		request.Headers.Range = range;
		return await _app.CreateClient().SendAsync(request, Ct);
	}

	[Fact]
	public async Task Needs_a_signed_in_user_or_the_api_key()
	{
		AddVideo("cube.mp4");
		Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync("/timelapse/cube.mp4", Ct)).StatusCode);
	}

	[Fact]
	public async Task Serves_the_video()
	{
		AddVideo("cube.mp4");

		HttpResponseMessage r = await Get("/timelapse/cube.mp4");

		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
		Assert.Equal("video/mp4", r.Content.Headers.ContentType?.MediaType);
		Assert.Equal(_video, await r.Content.ReadAsByteArrayAsync(Ct));
		Assert.Null(r.Content.Headers.ContentDisposition);
	}

	[Fact]
	public async Task Serves_ranges_so_the_player_can_seek()
	{
		AddVideo("cube.mp4");

		HttpResponseMessage r = await Get("/timelapse/cube.mp4", new RangeHeaderValue(10, 19));

		Assert.Equal(HttpStatusCode.PartialContent, r.StatusCode);
		Assert.Equal(_video[10..20], await r.Content.ReadAsByteArrayAsync(Ct));
	}

	[Fact]
	public async Task Download_names_the_file()
	{
		AddVideo("cube.mp4");

		HttpResponseMessage r = await Get("/timelapse/cube.mp4?download=true");

		Assert.Equal("cube.mp4", r.Content.Headers.ContentDisposition?.FileName);
	}

	[Theory]
	[InlineData("/timelapse/missing.mp4")]
	[InlineData("/timelapse/settings.json")]
	[InlineData("/timelapse/..%2Fsettings.json")]
	public async Task Anything_else_is_not_found(string url)
	{
		AddVideo("cube.mp4");
		Assert.Equal(HttpStatusCode.NotFound, (await Get(url)).StatusCode);
	}
}
