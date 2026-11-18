using Mediator;

using PrintDeck.Application.Features.Camera.Handlers;
using PrintDeck.Application.Features.Camera.Queries;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Web.Api;

/// <summary>
/// One still image from the configured snapshot URL, fetched by PrintDeck so the browser (and integrations with the
/// API key) don't need to reach the camera. Not rotated or flipped: those settings are applied where it is shown.
/// </summary>
public static class CameraApi
{
	public static void MapCameraApi(this WebApplication app) =>
		app.MapGet("/camera/snapshot", async (bool? download, HttpContext http, IMediator mediator, TimeProvider time, CancellationToken ct) =>
		{
			Result<CameraSnapshot> result = await mediator.Send(new GetCameraSnapshotQuery(), ct);
			http.Response.Headers.CacheControl = "no-store";
			if (!result.IsSuccess)
			{
				int status = result.Error == GetCameraSnapshotHandler.NotSet ? StatusCodes.Status404NotFound : StatusCodes.Status502BadGateway;
				return Results.Text(result.Error, "text/plain; charset=utf-8", statusCode: status);
			}

			CameraSnapshot snapshot = result.Value!;
			string? name = download == true
				? $"snapshot-{time.GetLocalNow():yyyyMMdd-HHmmss}{Extension(snapshot.ContentType)}"
				: null;
			return Results.File(snapshot.Content, snapshot.ContentType, name);
		});

	private static string Extension(string contentType) => contentType switch
	{
		"image/png" => ".png",
		"image/webp" => ".webp",
		"image/gif" => ".gif",
		_ => ".jpg"
	};
}
