using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.ApiKeys;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Web.Tests.Api;

/// <summary>OctoPrint's app-key flow as Home Assistant drives it: probe, request, poll until an admin answers.</summary>
public sealed class AppKeysApiTests : IAsyncDisposable
{
	private readonly PrintDeckWebFactory _app = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ValueTask DisposeAsync() => _app.DisposeAsync();

	private async Task<string> Request(string app = "Home Assistant", string? user = "ada")
	{
		HttpResponseMessage r = await _app.CreateClient().PostAsJsonAsync("/plugin/appkeys/request", new { app, user }, Ct);
		Assert.Equal(HttpStatusCode.Created, r.StatusCode);
		string location = r.Headers.Location!.ToString();
		string token = location[(location.LastIndexOf('/') + 1)..];
		Assert.Equal(token, (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("app_token").GetString());
		return token;
	}

	private Task<HttpResponseMessage> Poll(string token) => _app.CreateClient().GetAsync($"/plugin/appkeys/request/{token}", Ct);

	[Fact]
	public async Task Probe_says_app_keys_work()
	{
		Assert.Equal(HttpStatusCode.NoContent, (await _app.CreateClient().GetAsync("/plugin/appkeys/probe", Ct)).StatusCode);
	}

	[Fact]
	public async Task A_request_waits_then_hands_out_a_working_key_once()
	{
		await using (AsyncServiceScope scope = _app.Services.CreateAsyncScope())
			await scope.ServiceProvider.GetRequiredService<IUserAccounts>().CreateAsync("ada", "password123", UserRole.Admin, Ct);
		string token = await Request();

		HttpResponseMessage pending = await Poll(token);
		Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
		Assert.Equal(JsonValueKind.Object, (await pending.Content.ReadFromJsonAsync<JsonElement>(Ct)).ValueKind); // the client reads JSON here too

		// What allowing it in the banner does (the handler is tested on its own).
		string key = (await _app.Services.GetRequiredService<IApiKeys>().CreateAsync("ada", "Home Assistant", Ct))!;
		Assert.True(_app.Services.GetRequiredService<AppKeyRequests>().Grant(token, key));

		HttpResponseMessage granted = await Poll(token);
		Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
		string? handedOut = (await granted.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("api_key").GetString();
		Assert.Equal(key, handedOut);
		Assert.Equal(HttpStatusCode.NotFound, (await Poll(token)).StatusCode);

		HttpRequestMessage job = new(HttpMethod.Get, "/api/job");
		job.Headers.Add("X-Api-Key", handedOut);
		Assert.Equal(HttpStatusCode.OK, (await _app.CreateClient().SendAsync(job, Ct)).StatusCode);
	}

	[Fact]
	public async Task A_denied_or_unknown_request_is_not_found()
	{
		string token = await Request();
		Assert.True(_app.Services.GetRequiredService<AppKeyRequests>().Deny(token));

		Assert.Equal(HttpStatusCode.NotFound, (await Poll(token)).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await Poll("0123456789ABCDEF")).StatusCode);
	}

	[Fact]
	public async Task A_request_needs_an_app_name()
	{
		HttpResponseMessage r = await _app.CreateClient().PostAsJsonAsync("/plugin/appkeys/request", new { user = "ada" }, Ct);
		Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
	}

	[Fact]
	public async Task Too_many_waiting_requests_are_refused()
	{
		for (int i = 0; i < AppKeyRequests.MaxPending; i++) await Request($"App {i}");

		HttpResponseMessage r = await _app.CreateClient().PostAsJsonAsync("/plugin/appkeys/request", new { app = "One more" }, Ct);
		Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
	}
}
