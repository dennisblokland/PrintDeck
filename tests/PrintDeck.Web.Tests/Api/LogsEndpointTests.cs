using System.Net;

using PrintDeck.Web.Tests.Auth;

namespace PrintDeck.Web.Tests.Api;

/// <summary>Log downloads: signed-in admins only, listed files only.</summary>
public sealed class LogsEndpointTests : IAsyncDisposable
{
	private readonly PrintDeckWebFactory _app = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ValueTask DisposeAsync() => _app.DisposeAsync();

	private async Task<BrowserSession> AdminAsync()
	{
		BrowserSession browser = new(_app);
		await browser.SetUp();
		string logs = Path.Combine(_app.DataDirectory, "logs");
		Directory.CreateDirectory(logs);
		await File.WriteAllTextAsync(Path.Combine(logs, "serial.log"), "> N1 M105*39\n", Ct);
		return browser;
	}

	[Fact]
	public async Task An_admin_downloads_a_log_file()
	{
		BrowserSession admin = await AdminAsync();

		HttpResponseMessage r = await admin.Client.GetAsync("/logs/serial.log", Ct);

		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
		Assert.Equal("text/plain", r.Content.Headers.ContentType?.MediaType);
		Assert.Equal("serial.log", r.Content.Headers.ContentDisposition?.FileName);
		Assert.Equal("> N1 M105*39\n", await r.Content.ReadAsStringAsync(Ct));
	}

	[Theory]
	[InlineData("/logs/missing.log")]
	[InlineData("/logs/..%2Fsettings.json")]
	[InlineData("/logs/printdeck.db")]
	public async Task Other_files_are_not_found(string url)
	{
		BrowserSession admin = await AdminAsync();
		Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.GetAsync(url, Ct)).StatusCode);
	}

	[Fact]
	public async Task The_api_key_is_refused()
	{
		await AdminAsync();
		HttpRequestMessage request = new(HttpMethod.Get, "/logs/serial.log");
		request.Headers.Add("X-Api-Key", await _app.ApiKeyAsync());

		HttpResponseMessage r = await _app.CreateClient().SendAsync(request, Ct);

		Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
	}

	[Fact]
	public async Task Strangers_are_refused()
	{
		await AdminAsync();
		HttpResponseMessage r = await _app.CreateClient().GetAsync("/logs/serial.log", Ct);
		Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
	}
}
