using System.IO.Compression;
using System.Net;
using System.Text;

using PrintDeck.Web.Tests.Auth;

namespace PrintDeck.Web.Tests.Api;

/// <summary>Backup download: signed-in admins only, since the archive holds the API key.</summary>
public sealed class BackupEndpointTests : IAsyncDisposable
{
	private readonly PrintDeckWebFactory _app = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ValueTask DisposeAsync() => _app.DisposeAsync();

	private async Task<BrowserSession> AdminAsync()
	{
		BrowserSession browser = new(_app);
		await browser.SetUp();
		await _app.Files.SaveAsync("cube.gcode", new MemoryStream(Encoding.UTF8.GetBytes("G28\n")), Ct);
		return browser;
	}

	[Fact]
	public async Task An_admin_downloads_a_zip_with_settings_and_files()
	{
		BrowserSession admin = await AdminAsync();

		HttpResponseMessage r = await admin.Client.GetAsync("/backup", Ct);

		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
		Assert.Equal("application/zip", r.Content.Headers.ContentType?.MediaType);
		Assert.Matches(@"^printdeck-backup-\d{8}-\d{4}\.zip$", r.Content.Headers.ContentDisposition?.FileName);
		using ZipArchive zip = new(new MemoryStream(await r.Content.ReadAsByteArrayAsync(Ct)), ZipArchiveMode.Read);
		Assert.Equal(["printdeck-backup.json", "settings.json", "uploads/cube.gcode"], zip.Entries.Select(e => e.FullName));
		using StreamReader settings = new(zip.GetEntry("settings.json")!.Open());
		Assert.Contains(await _app.ApiKeyAsync(), await settings.ReadToEndAsync(Ct));
	}

	[Fact]
	public async Task The_api_key_is_refused()
	{
		await AdminAsync();
		HttpRequestMessage request = new(HttpMethod.Get, "/backup");
		request.Headers.Add("X-Api-Key", await _app.ApiKeyAsync());

		HttpResponseMessage r = await _app.CreateClient().SendAsync(request, Ct);

		Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
	}

	[Fact]
	public async Task Strangers_are_refused()
	{
		await AdminAsync();
		HttpResponseMessage r = await _app.CreateClient().GetAsync("/backup", Ct);
		Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
	}
}
