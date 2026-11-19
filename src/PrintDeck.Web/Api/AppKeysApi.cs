using System.Text.Json;

using Mediator;

using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Application.Features.ApiKeys.Dtos;
using PrintDeck.Application.Features.ApiKeys.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Web.Api;

/// <summary>
/// OctoPrint's app-key flow (<c>/plugin/appkeys</c>), which Home Assistant uses to get its key: the app asks, an admin
/// allows it in PrintDeck (a banner on every page), and the app picks the key up by polling. No sign-in: the app has no
/// key yet, and asking gives it nothing until an admin says yes.
/// </summary>
public static class AppKeysApi
{
	public static void MapAppKeysApi(this WebApplication app)
	{
		RouteGroupBuilder appkeys = app.MapGroup("/plugin/appkeys").AllowAnonymous();

		appkeys.MapGet("/probe", () => Results.NoContent());

		appkeys.MapPost("/request", async (JsonElement body, HttpRequest http, IMediator mediator, CancellationToken ct) =>
		{
			string? name = Text(body, "app");
			Result<AppKeyRequest> made = await mediator.Send(new RequestAppKeyCommand(name ?? "", Text(body, "user")), ct);
			if (!made.IsSuccess)
				return made.Error == AppKeyRequests.TooMany
					? Results.Json(new { error = made.Error }, statusCode: StatusCodes.Status429TooManyRequests)
					: Results.BadRequest(new { error = made.Error });

			string token = made.Value!.Token;
			return Results.Created($"{http.PathBase}/plugin/appkeys/request/{token}", new { app_token = token });
		}).DisableAntiforgery();

		appkeys.MapGet("/request/{token}", async (string token, IMediator mediator, CancellationToken ct) =>
		{
			AppKeyPoll poll = await mediator.Send(new PollAppKeyRequestQuery(token), ct);
			return poll.State switch
			{
				AppKeyRequestState.Pending => Results.Json(new { }, statusCode: StatusCodes.Status202Accepted),
				AppKeyRequestState.Granted => Results.Ok(new { api_key = poll.Key }),
				// With a body, so the app's status-code pages leave it alone (they would answer with the sign-in page).
				_ => Results.Json(new { error = "No such request: it was denied, expired or already answered." }, statusCode: StatusCodes.Status404NotFound)
			};
		});
	}

	private static string? Text(JsonElement body, string name) =>
		body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
