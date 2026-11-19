using System.Text.Json;

using Mediator;

using PrintDeck.Application.Behaviors;
using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Application.Features.Connection.Handlers;
using PrintDeck.Application.Features.Connection.Queries;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Features.SdCard.Dtos;
using PrintDeck.Application.Features.SdCard.Queries;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Settings;
using PrintDeck.Web.Auth;

namespace PrintDeck.Web.Api;

/// <summary>
/// The status and control part of OctoPrint's REST API, for OctoPrint clients such as Home Assistant's OctoPrint
/// integration: <c>/api/printer</c>, <c>/api/job</c>, <c>/api/connection</c>, <c>/api/settings</c> and <c>/api/server</c>.
/// Same key as the slicer API. Reads work with any key; control needs a key of an admin account, as everywhere.
/// </summary>
public static class OctoPrintApi
{
	public const string NoSelectedFile = "PrintDeck has no selected file: start prints from the Files page or a slicer upload.";

	public static void MapOctoPrintApi(this WebApplication app)
	{
		RouteGroupBuilder api = app.MapGroup("/api").RequireAuthorization(AuthSchemes.ApiKeyPolicy);

		api.MapGet("/server", () => Results.Ok(new { version = OctoPrintModel.Version, safemode = (string?) null }));

		api.MapGet("/printer", async (IMediator mediator, CancellationToken ct) =>
		{
			PrinterStatusDto status = await mediator.Send(new GetPrinterStatusQuery(), ct);
			// OctoPrint answers 409 while the printer is not connected; Home Assistant keeps its last reading then.
			if (!OctoPrintModel.IsOperational(status.State)) return Results.Conflict("Printer is not operational");
			SdCardDto sd = await mediator.Send(new GetSdCardQuery(), ct);
			return Results.Ok(OctoPrintModel.Printer(status, sd.State));
		});

		api.MapGet("/job", async (IMediator mediator, CancellationToken ct) =>
		{
			PrinterStatusDto status = await mediator.Send(new GetPrinterStatusQuery(), ct);
			GcodeFile? file = status.Job is { Kind: JobKind.Print } job ? await mediator.Send(new GetGcodeFileQuery(job.FileName), ct) : null;
			return Results.Ok(OctoPrintModel.Job(status, file?.SizeBytes, file?.Analysis?.EstimatedPrintTime));
		});

		api.MapPost("/job", async (JsonElement body, IMediator mediator, CancellationToken ct) =>
		{
			string? command = Text(body, "command");
			string action = Text(body, "action") ?? "toggle";
			PrinterStatusDto status = await mediator.Send(new GetPrinterStatusQuery(), ct);
			ICommand<Result>? message = (command, action) switch
			{
				("cancel", _) => new CancelJobCommand(),
				("pause", "pause") => new PauseJobCommand(),
				("pause", "resume") => new ResumeJobCommand(),
				("pause", "toggle") => status.Job?.Status == JobStatus.Paused ? new ResumeJobCommand() : new PauseJobCommand(),
				_ => null
			};
			if (command is "start" or "restart") return Results.Conflict(new { error = NoSelectedFile });
			if (message is null) return Results.BadRequest(new { error = "Unknown job command." });
			return Answer(await mediator.Send(message, ct));
		});

		api.MapGet("/connection", async (IMediator mediator, CancellationToken ct) =>
		{
			PrinterStatusDto status = await mediator.Send(new GetPrinterStatusQuery(), ct);
			HostSettings settings = await mediator.Send(new GetSettingsQuery(), ct);
			IReadOnlyList<string> ports = await mediator.Send(new ListPortsQuery(), ct);
			return Results.Ok(OctoPrintModel.Connection(status, settings, ports, ConnectPrinterHandler.SupportedBaudRates));
		});

		api.MapPost("/connection", async (JsonElement body, IMediator mediator, CancellationToken ct) =>
		{
			ICommand<Result>? message = Text(body, "command") switch
			{
				"connect" when Text(body, "port") is { Length: > 0 } port =>
					new ConnectPrinterCommand(port, Number(body, "baudrate") ?? (await mediator.Send(new GetSettingsQuery(), ct)).DefaultBaudRate),
				"connect" => new ConnectLastPrinterCommand(),
				"disconnect" => new DisconnectPrinterCommand(),
				_ => null
			};
			return message is null ? Results.BadRequest(new { error = "Unknown connection command." }) : Answer(await mediator.Send(message, ct));
		});

		api.MapGet("/settings", async (IMediator mediator, CancellationToken ct) =>
			Results.Ok(OctoPrintModel.Settings(await mediator.Send(new GetSettingsQuery(), ct))));
	}

	/// <summary>OctoPrint's answers: 204 when done, 403 for a key that may only watch, 409 when the printer can't do it now.</summary>
	private static IResult Answer(Result result) =>
		result.IsSuccess ? Results.NoContent()
		: result.Error == AuthorizationBehavior<PauseJobCommand, Result>.AdminRequired
			? Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status403Forbidden)
			: Results.Conflict(new { error = result.Error });

	private static string? Text(JsonElement body, string name) =>
		body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

	private static int? Number(JsonElement body, string name) =>
		body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out JsonElement v) && v.TryGetInt32(out int n) ? n : null;
}
