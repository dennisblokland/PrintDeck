using Mediator;

using PrintDeck.Application.Features.Files.Dtos;
using PrintDeck.Application.Features.Files.Queries;

namespace PrintDeck.Web.Api;

/// <summary>
/// Serves a stored file's slicer thumbnail (PNG or JPEG). Same-origin UI endpoint: needs the login cookie (or the API key).
/// The ETag follows the upload, so a re-uploaded file gets its new picture.
/// </summary>
public static class ThumbnailApi
{
	public static void MapThumbnailApi(this WebApplication app) =>
		app.MapGet("/thumbnail/{name}", async (string name, HttpContext http, IMediator mediator, CancellationToken ct) =>
		{
			ThumbnailContent? thumbnail = await mediator.Send(new GetGcodeThumbnailQuery(name), ct);
			if (thumbnail is null) return Results.NotFound();

			string etag = $"\"{thumbnail.File.UploadedAt.UtcTicks:x}-{thumbnail.File.SizeBytes:x}\"";
			http.Response.Headers.CacheControl = "no-cache";
			http.Response.Headers.ETag = etag;
			if (http.Request.Headers.IfNoneMatch == etag)
			{
				await thumbnail.Content.DisposeAsync();
				return Results.StatusCode(StatusCodes.Status304NotModified);
			}

			return Results.Stream(thumbnail.Content, thumbnail.ContentType);
		});
}
