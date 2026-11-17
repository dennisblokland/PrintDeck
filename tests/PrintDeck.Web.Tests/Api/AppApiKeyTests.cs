using System.Net;
using System.Net.Http.Json;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Web.Tests.Api;

/// <summary>App keys at the slicer API: each acts as its owner, and stops the moment it is revoked.</summary>
public sealed class AppApiKeyTests : IAsyncDisposable
{
	private readonly PrintDeckWebFactory _app = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ValueTask DisposeAsync() => _app.DisposeAsync();

	private sealed record ErrorBody(string Error);

	private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> work)
	{
		await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
		return await work(scope.ServiceProvider);
	}

	private async Task<string> KeyFor(string userName, UserRole role, string keyName)
	{
		await InScope(sp => sp.GetRequiredService<IUserAccounts>().CreateAsync(userName, "password123", role, Ct));
		return (await InScope(sp => sp.GetRequiredService<IApiKeys>().CreateAsync(userName, keyName, Ct)))!;
	}

	private Task<HttpResponseMessage> Get(string url, string key)
	{
		HttpRequestMessage request = new(HttpMethod.Get, url);
		request.Headers.Add("X-Api-Key", key);
		return _app.CreateClient().SendAsync(request, Ct);
	}

	private Task<HttpResponseMessage> Upload(string key)
	{
		HttpRequestMessage request = new(HttpMethod.Post, "/api/files/local");
		request.Headers.Add("X-Api-Key", key);
		request.Content = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes("G28\n")), "file", "cube.gcode" } };
		return _app.CreateClient().SendAsync(request, Ct);
	}

	[Fact]
	public async Task An_admins_key_uploads()
	{
		string key = await KeyFor("ada", UserRole.Admin, "OrcaSlicer");

		Assert.Equal(HttpStatusCode.OK, (await Get("/api/version", key)).StatusCode);
		Assert.Equal(HttpStatusCode.Created, (await Upload(key)).StatusCode);
		Assert.NotNull(await _app.Files.GetAsync("cube.gcode", Ct));
	}

	[Fact]
	public async Task A_viewers_key_can_look_but_not_upload()
	{
		string key = await KeyFor("vic", UserRole.Viewer, "Dashboard");

		Assert.Equal(HttpStatusCode.OK, (await Get("/api/version", key)).StatusCode);
		HttpResponseMessage r = await Upload(key);

		Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
		Assert.Equal("Only admins can do that.", (await r.Content.ReadFromJsonAsync<ErrorBody>(Ct))!.Error);
		Assert.Null(await _app.Files.GetAsync("cube.gcode", Ct));
	}

	[Fact]
	public async Task A_revoked_key_is_refused()
	{
		string key = await KeyFor("ada", UserRole.Admin, "OrcaSlicer");
		string id = Assert.Single(await InScope(sp => sp.GetRequiredService<IApiKeys>().ListAsync(Ct))).Id;

		await InScope(sp => sp.GetRequiredService<IApiKeys>().RevokeAsync(id, Ct));

		Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/version", key)).StatusCode);
	}

	[Fact]
	public async Task With_the_shared_key_off_only_app_keys_work()
	{
		string shared = await _app.ApiKeyAsync();
		string key = await KeyFor("ada", UserRole.Admin, "OrcaSlicer");

		await _app.Services.GetRequiredService<ISettingsStore>().UpdateAsync(s => s with { SharedApiKeyEnabled = false }, Ct);

		Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/version", shared)).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await Get("/api/version", key)).StatusCode);
	}

	[Fact]
	public async Task An_app_key_is_no_account_for_host_administration()
	{
		string key = await KeyFor("ada", UserRole.Admin, "OrcaSlicer");
		Assert.Equal(HttpStatusCode.Forbidden, (await Get("/backup", key)).StatusCode);
	}
}
