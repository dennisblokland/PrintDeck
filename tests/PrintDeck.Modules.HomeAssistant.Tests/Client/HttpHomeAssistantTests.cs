using System.Net;

using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Modules.HomeAssistant.Tests.TestDoubles;
using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Tests.Client;

public sealed class HttpHomeAssistantTests
{
	private readonly StubHttpHandler _handler = new();
	private readonly HttpHomeAssistant _ha;

	public HttpHomeAssistantTests() =>
		_ha = new HttpHomeAssistant(new HttpClient(_handler), new Uri("http://ha.local:8123"), "TOKEN", TimeSpan.FromMilliseconds(200));

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Reads_an_entity_with_the_token()
	{
		_handler.Respond = (_, _) => Task.FromResult(StubHttpHandler.Json(
			"""{"entity_id":"light.printer","state":"on","attributes":{"friendly_name":"Printer light"}}"""));

		PluginResult<EntityState> state = await _ha.GetStateAsync("light.printer", Ct);

		Assert.True(state.IsSuccess);
		Assert.Equal(new EntityState("light.printer", "on", "Printer light"), state.Value);
		Assert.True(state.Value!.IsOn);
		(HttpMethod method, string path, string? auth, _) = Assert.Single(_handler.Requests);
		Assert.Equal(HttpMethod.Get, method);
		Assert.Equal("/api/states/light.printer", path);
		Assert.Equal("Bearer TOKEN", auth);
	}

	[Fact]
	public async Task Calls_a_service_for_one_entity()
	{
		_handler.Respond = (_, _) => Task.FromResult(StubHttpHandler.Json("[]"));

		PluginResult called = await _ha.CallServiceAsync("switch", "turn_off", "switch.lamp", Ct);

		Assert.True(called.IsSuccess);
		(HttpMethod method, string path, _, string? body) = Assert.Single(_handler.Requests);
		Assert.Equal(HttpMethod.Post, method);
		Assert.Equal("/api/services/switch/turn_off", path);
		Assert.Equal("""{"entity_id":"switch.lamp"}""", body);
	}

	[Fact]
	public async Task Lists_every_entity()
	{
		_handler.Respond = (_, _) => Task.FromResult(StubHttpHandler.Json(
			"""[{"entity_id":"light.printer","state":"off","attributes":{}},{"entity_id":"sensor.t","state":"21","attributes":{"friendly_name":"T"}}]"""));

		PluginResult<IReadOnlyList<EntityState>> states = await _ha.ListStatesAsync(Ct);

		Assert.Equal(["light.printer", "sensor.t"], states.Value!.Select(s => s.EntityId));
	}

	[Theory]
	[InlineData(HttpStatusCode.Unauthorized, HomeAssistantErrors.RefusedToken)]
	[InlineData(HttpStatusCode.Forbidden, HomeAssistantErrors.RefusedToken)]
	[InlineData(HttpStatusCode.NotFound, "Home Assistant has no light.nope.")]
	[InlineData(HttpStatusCode.InternalServerError, "Home Assistant answered 500.")]
	public async Task Says_what_went_wrong(HttpStatusCode status, string error)
	{
		_handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status));

		PluginResult<EntityState> state = await _ha.GetStateAsync("light.nope", Ct);

		Assert.False(state.IsSuccess);
		Assert.Equal(error, state.Error);
	}

	[Fact]
	public async Task Gives_up_when_home_assistant_does_not_answer()
	{
		_handler.Respond = async (_, ct) =>
		{
			await Task.Delay(Timeout.Infinite, ct);
			return new HttpResponseMessage(HttpStatusCode.OK);
		};

		PluginResult<EntityState> state = await _ha.GetStateAsync("light.printer", Ct);

		Assert.Equal(HomeAssistantErrors.NoAnswer, state.Error);
	}

	[Fact]
	public async Task A_refused_connection_is_no_answer()
	{
		_handler.Respond = (_, _) => throw new HttpRequestException("Connection refused");

		PluginResult called = await _ha.CallServiceAsync("light", "turn_on", "light.printer", Ct);

		Assert.Equal(HomeAssistantErrors.NoAnswer, called.Error);
	}

	[Fact]
	public async Task Keeps_a_path_in_the_url()
	{
		HttpHomeAssistant behindProxy = new(new HttpClient(_handler), new Uri("https://home.example/ha"), "T");
		_handler.Respond = (_, _) => Task.FromResult(StubHttpHandler.Json("[]"));

		await behindProxy.ListStatesAsync(Ct);

		Assert.Equal("/ha/api/states", Assert.Single(_handler.Requests).Path);
	}
}
