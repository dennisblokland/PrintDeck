using Mediator;

using PrintDeck.Application.Features.Files;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Features.Jobs;
using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Features.Jobs.Handlers;
using PrintDeck.Application.Features.Settings;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Settings;
using PrintDeck.Web.Auth;

namespace PrintDeck.Web.Api;

/// <summary>
/// Subset of the OctoPrint REST API that slicers (OrcaSlicer, PrusaSlicer, Cura) use for
/// "send to printer": version probe + file upload with optional auto-start.
/// Pick host type "OctoPrint" in the slicer and paste PrintDeck's API key.
/// </summary>
public static class SlicerApi
{
	public static void MapSlicerApi(this WebApplication app)
	{
		// Only the API key opens this group (see ApiKeyAuthenticationHandler); a missing or wrong key gets OctoPrint's 403.
		RouteGroupBuilder api = app.MapGroup("/api").RequireAuthorization(AuthSchemes.ApiKeyPolicy);

		// Slicers check that "text" starts with "OctoPrint" before uploading.
		api.MapGet("/version", () => Results.Ok(new { api = "0.1", server = "1.10.0", text = "OctoPrint 1.10.0 (PrintDeck)" }));

		api.MapPost("/files/local", async (HttpRequest request, IMediator mediator, CancellationToken ct) =>
		{
			IFormCollection form = await request.ReadFormAsync(ct);
			if (form.Files.GetFile("file") is not { } file) return Results.BadRequest(new { error = "No file" });

			await using Stream stream = file.OpenReadStream();
			Result<GcodeFile> upload = await mediator.Send(new UploadGcodeCommand(file.FileName, stream), ct);
			if (!upload.IsSuccess) return Results.BadRequest(new { error = upload.Error });

			string name = upload.Value!.Name;
			if (IsTrue(form["print"]))
			{
				Result start = await mediator.Send(new StartPrintCommand(name), ct);
				if (!start.IsSuccess) return Results.Conflict(new { error = start.Error });
			}

			return Results.Created($"/api/files/local/{Uri.EscapeDataString(name)}", new
			{
				done = true,
				files = new { local = new { name, origin = "local" } }
			});
		}).DisableAntiforgery();
	}

	private static bool IsTrue(string? value) => value is "true" or "True" or "1";
}
