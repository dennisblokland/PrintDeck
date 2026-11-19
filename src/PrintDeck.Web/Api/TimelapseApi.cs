using Mediator;

using PrintDeck.Application.Features.Timelapse.Queries;

namespace PrintDeck.Web.Api;

/// <summary>
/// Serves a rendered timelapse (MP4) to signed-in users or the API key, with range requests so the browser can seek.
/// <c>?download=true</c> saves it as a file.
/// </summary>
public static class TimelapseApi
{
	public static void MapTimelapseApi(this WebApplication app) =>
		app.MapGet("/timelapse/{name}", async (string name, bool? download, IMediator mediator, CancellationToken ct) =>
			await mediator.Send(new GetTimelapseVideoQuery(name), ct) is { } video
				? Results.Stream(video, "video/mp4", download == true ? name : null, enableRangeProcessing: true)
				: Results.NotFound());
}
