using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Client;

/// <summary>Home Assistant's REST API with a long-lived access token. Each call gives up after <see cref="Timeout"/>.</summary>
public sealed class HttpHomeAssistant(HttpClient http, Uri baseUrl, string token, TimeSpan? timeout = null) : IHomeAssistant
{
	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

	private readonly Uri _base = baseUrl.AbsoluteUri.EndsWith('/') ? baseUrl : new Uri(baseUrl.AbsoluteUri + "/");
	private readonly TimeSpan _timeout = timeout ?? Timeout;

	private sealed record StateJson(
		[property: JsonPropertyName("entity_id")] string EntityId,
		[property: JsonPropertyName("state")] string State,
		[property: JsonPropertyName("attributes")] AttributesJson? Attributes);

	private sealed record AttributesJson([property: JsonPropertyName("friendly_name")] string? FriendlyName);

	public async Task<PluginResult<EntityState>> GetStateAsync(string entityId, CancellationToken ct)
	{
		PluginResult<StateJson> state = await SendAsync<StateJson>(HttpMethod.Get, $"api/states/{Uri.EscapeDataString(entityId)}", null, entityId, ct);
		return state.IsSuccess ? PluginResult.Success(ToEntity(state.Value!)) : PluginResult.Failure<EntityState>(state.Error!);
	}

	public async Task<PluginResult<IReadOnlyList<EntityState>>> ListStatesAsync(CancellationToken ct)
	{
		PluginResult<StateJson[]> states = await SendAsync<StateJson[]>(HttpMethod.Get, "api/states", null, null, ct);
		return states.IsSuccess
			? PluginResult.Success<IReadOnlyList<EntityState>>(states.Value!.Select(ToEntity).ToList())
			: PluginResult.Failure<IReadOnlyList<EntityState>>(states.Error!);
	}

	public async Task<PluginResult> CallServiceAsync(string domain, string service, string entityId, CancellationToken ct)
	{
		PluginResult<JsonElement> called = await SendAsync<JsonElement>(HttpMethod.Post,
			$"api/services/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(service)}",
			JsonContent.Create(new Dictionary<string, string> { ["entity_id"] = entityId }), entityId, ct);
		return called.IsSuccess ? PluginResult.Success() : PluginResult.Failure(called.Error!);
	}

	private static EntityState ToEntity(StateJson s) => new(s.EntityId, s.State, s.Attributes?.FriendlyName);

	/// <param name="entityId">The entity a 404 is about; null when a 404 means a wrong URL.</param>
	private async Task<PluginResult<T>> SendAsync<T>(HttpMethod method, string path, HttpContent? body, string? entityId, CancellationToken ct)
	{
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(_timeout);
		using HttpRequestMessage request = new(method, new Uri(_base, path)) { Content = body };
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		try
		{
			using HttpResponseMessage response = await http.SendAsync(request, timeout.Token);
			if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
				return PluginResult.Failure<T>(HomeAssistantErrors.RefusedToken);
			if (response.StatusCode == HttpStatusCode.NotFound && entityId is not null)
				return PluginResult.Failure<T>(HomeAssistantErrors.NoEntity(entityId));
			if (!response.IsSuccessStatusCode)
				return PluginResult.Failure<T>(HomeAssistantErrors.Answered((int) response.StatusCode));
			T? value = await response.Content.ReadFromJsonAsync<T>(timeout.Token);
			return value is null ? PluginResult.Failure<T>(HomeAssistantErrors.NoAnswer) : PluginResult.Success(value);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			return PluginResult.Failure<T>(HomeAssistantErrors.NoAnswer);
		}
		catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
		{
			return PluginResult.Failure<T>(HomeAssistantErrors.NoAnswer);
		}
	}
}
