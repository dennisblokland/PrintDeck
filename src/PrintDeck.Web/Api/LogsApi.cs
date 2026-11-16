using Mediator;

using PrintDeck.Application.Features.Logs.Queries;

namespace PrintDeck.Web.Api;

/// <summary>Log file downloads for the Terminal page: signed-in admin accounts only (not the API key).</summary>
public static class LogsApi
{
	public static void MapLogsApi(this WebApplication app) =>
		app.MapGet("/logs/{name}", async (string name, IMediator mediator, CancellationToken ct) =>
		{
			Stream? stream;
			try
			{
				stream = await mediator.Send(new OpenLogFileQuery(name), ct);
			}
			catch (UnauthorizedAccessException)
			{
				return Results.StatusCode(StatusCodes.Status403Forbidden);
			}
			return stream is null ? Results.NotFound() : Results.File(stream, "text/plain; charset=utf-8", name);
		});
}
