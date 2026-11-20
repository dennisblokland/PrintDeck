using System.Net;
using System.Text;

namespace PrintDeck.Modules.HomeAssistant.Tests.TestDoubles;

/// <summary>Answers each request with <see cref="Respond"/> and remembers what was asked.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
	public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
		(_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

	public List<(HttpMethod Method, string Path, string? Authorization, string? Body)> Requests { get; } = [];

	public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
		new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
	{
		string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
		Requests.Add((request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString(), body));
		return await Respond(request, ct);
	}
}
