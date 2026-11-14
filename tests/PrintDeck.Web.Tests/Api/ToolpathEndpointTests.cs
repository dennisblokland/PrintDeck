using System.Net;
using System.Text;

namespace PrintDeck.Web.Tests.Api;

public class ToolpathEndpointTests(PrintDeckWebFactory app) : IClassFixture<PrintDeckWebFactory>
{
	private readonly HttpClient _client = app.CreateClient();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Unknown_files_are_not_found()
	{
		HttpResponseMessage r = await _client.GetAsync("/toolpath/missing.gcode", Ct);
		Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
	}

	[Fact]
	public async Task Serves_the_binary_toolpath_with_an_etag()
	{
		await app.Files.SaveAsync("tp.gcode", new MemoryStream("G1 X0 Y0\nG1 X10 Y0 E1\n"u8.ToArray()), Ct);

		HttpResponseMessage r = await _client.GetAsync("/toolpath/tp.gcode", Ct);

		r.EnsureSuccessStatusCode();
		Assert.Equal("application/octet-stream", r.Content.Headers.ContentType?.MediaType);
		Assert.Equal("PDTP", Encoding.ASCII.GetString((await r.Content.ReadAsByteArrayAsync(Ct))[..4]));
		Assert.NotNull(r.Headers.ETag);

		HttpRequestMessage again = new(HttpMethod.Get, "/toolpath/tp.gcode");
		again.Headers.IfNoneMatch.Add(r.Headers.ETag!);
		Assert.Equal(HttpStatusCode.NotModified, (await _client.SendAsync(again, Ct)).StatusCode);
	}
}
