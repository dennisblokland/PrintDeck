using System.Net;
using System.Net.Http.Headers;

using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;
using PrintDeck.Infrastructure.Camera;

namespace PrintDeck.Infrastructure.Tests.Camera;

public class HttpCameraSnapshotsTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;
	private static readonly Uri _url = new("http://cam.local/?action=snapshot");

	private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		public List<Uri?> Requested { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			Requested.Add(request.RequestUri);
			return Task.FromResult(respond(request));
		}
	}

	private static HttpResponseMessage Image(byte[] body, string type = "image/jpeg")
	{
		ByteArrayContent content = new(body);
		content.Headers.ContentType = new MediaTypeHeaderValue(type);
		return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
	}

	private static Task<Result<CameraSnapshot>> Take(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
		new HttpCameraSnapshots(new StubHandler(respond)).TakeAsync(_url, Ct);

	[Fact]
	public async Task Returns_the_image_and_its_type()
	{
		StubHandler handler = new(_ => Image([1, 2, 3], "image/JPEG"));

		Result<CameraSnapshot> r = await new HttpCameraSnapshots(handler).TakeAsync(_url, Ct);

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal([1, 2, 3], r.Value!.Content);
		Assert.Equal("image/jpeg", r.Value.ContentType);
		Assert.Equal([_url], handler.Requested);
	}

	[Fact]
	public async Task Reports_an_http_error()
	{
		Assert.Equal("The camera answered with HTTP 404.", (await Take(_ => new HttpResponseMessage(HttpStatusCode.NotFound))).Error);
	}

	[Fact]
	public async Task Refuses_something_that_is_not_an_image()
	{
		HttpResponseMessage Html(HttpRequestMessage _) => new(HttpStatusCode.OK) { Content = new StringContent("<html>", null, "text/html") };

		Assert.Equal("The snapshot URL did not return an image.", (await Take(Html)).Error);
		Assert.Equal("The snapshot URL did not return an image.", (await Take(_ => Image([]))).Error);
	}

	[Fact]
	public async Task Refuses_an_image_that_is_too_large()
	{
		Assert.Equal("The snapshot is too large.", (await Take(_ => Image(new byte[HttpCameraSnapshots.MaxBytes + 1]))).Error);
	}

	[Fact]
	public async Task Reports_an_unreachable_camera()
	{
		Assert.Equal("The camera did not answer.", (await Take(_ => throw new HttpRequestException("refused"))).Error);
	}
}
