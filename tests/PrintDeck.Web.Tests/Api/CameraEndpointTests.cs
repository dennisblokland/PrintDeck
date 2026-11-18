using System.Net;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Web.Tests.Api;

public sealed class CameraEndpointTests : IAsyncDisposable
{
	private sealed class StubCamera : ICameraSnapshots
	{
		public Result<CameraSnapshot> Next { get; set; } = Result.Success(new CameraSnapshot([0xFF, 0xD8, 0xFF, 0xE0], "image/jpeg"));

		public Task<Result<CameraSnapshot>> TakeAsync(Uri url, CancellationToken ct) => Task.FromResult(Next);
	}

	private readonly StubCamera _camera = new();
	private readonly PrintDeckWebFactory _app;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public CameraEndpointTests() =>
		_app = new PrintDeckWebFactory { ConfigureHost = b => b.ConfigureTestServices(s => s.AddSingleton<ICameraSnapshots>(_camera)) };

	public async ValueTask DisposeAsync() => await _app.DisposeAsync();

	private Task SetSnapshotUrl(string? url) =>
		_app.Services.GetRequiredService<ISettingsStore>().UpdateAsync(s => s with { WebcamSnapshotUrl = url }, Ct);

	private async Task<HttpResponseMessage> Get(string url)
	{
		HttpRequestMessage request = new(HttpMethod.Get, url);
		request.Headers.Add("X-Api-Key", await _app.ApiKeyAsync());
		return await _app.CreateClient().SendAsync(request, Ct);
	}

	[Fact]
	public async Task Needs_a_signed_in_user_or_the_api_key()
	{
		Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync("/camera/snapshot", Ct)).StatusCode);
	}

	[Fact]
	public async Task Serves_the_snapshot_uncached()
	{
		await SetSnapshotUrl("http://cam.local/?action=snapshot");

		HttpResponseMessage r = await Get("/camera/snapshot");

		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
		Assert.Equal("image/jpeg", r.Content.Headers.ContentType?.MediaType);
		Assert.Equal([0xFF, 0xD8, 0xFF, 0xE0], await r.Content.ReadAsByteArrayAsync(Ct));
		Assert.True(r.Headers.CacheControl?.NoStore);
		Assert.Null(r.Content.Headers.ContentDisposition);
	}

	[Fact]
	public async Task Download_names_the_file()
	{
		await SetSnapshotUrl("http://cam.local/?action=snapshot");

		HttpResponseMessage r = await Get("/camera/snapshot?download=true");

		Assert.Matches(@"^snapshot-\d{8}-\d{6}\.jpg$", r.Content.Headers.ContentDisposition?.FileName);
	}

	[Fact]
	public async Task Not_found_without_a_snapshot_url()
	{
		HttpResponseMessage r = await Get("/camera/snapshot");

		Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
		Assert.Equal("No snapshot URL is set.", await r.Content.ReadAsStringAsync(Ct));
	}

	[Fact]
	public async Task Bad_gateway_when_the_camera_fails()
	{
		await SetSnapshotUrl("http://cam.local/?action=snapshot");
		_camera.Next = Result.Failure<CameraSnapshot>("The camera did not answer.");

		HttpResponseMessage r = await Get("/camera/snapshot");

		Assert.Equal(HttpStatusCode.BadGateway, r.StatusCode);
		Assert.Equal("The camera did not answer.", await r.Content.ReadAsStringAsync(Ct));
	}
}
