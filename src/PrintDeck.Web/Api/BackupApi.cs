using Mediator;

using PrintDeck.Application.Features.Backup.Dtos;
using PrintDeck.Application.Features.Backup.Queries;

namespace PrintDeck.Web.Api;

/// <summary>Backup download for the Settings page: signed-in admin accounts only (the archive holds the API key).</summary>
public static class BackupApi
{
	public static void MapBackupApi(this WebApplication app) =>
		app.MapGet("/backup", async (IMediator mediator, CancellationToken ct) =>
		{
			BackupDownload download;
			try
			{
				download = await mediator.Send(new GetBackupQuery(), ct);
			}
			catch (UnauthorizedAccessException)
			{
				return Results.StatusCode(StatusCodes.Status403Forbidden);
			}
			return Results.Stream(body => download.WriteAsync(body, ct), "application/zip", download.FileName);
		});
}
