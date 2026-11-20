using System.Text.Json.Nodes;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Connection.Dtos;
using PrintDeck.Application.Features.Connection.Queries;
using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Features.Jobs.Commands;
using PrintDeck.Application.Features.Modules.Commands;
using PrintDeck.Application.Features.Modules.Dtos;
using PrintDeck.Application.Features.Modules.Queries;
using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Plugins;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Application.Plugins;

/// <summary>
/// <see cref="IPluginContext"/> on top of PrintDeck's own commands and queries, sent through the scope's mediator so
/// <c>AuthorizationBehavior</c> checks them for whoever the scope acts for. Disposing it ends its subscriptions.
/// </summary>
public sealed class PluginContext(string moduleId, IMediator mediator, ICurrentUser currentUser, PrinterEventStream stream)
	: IPluginContext, IDisposable
{
	private readonly Lock _gate = new();
	private readonly List<IDisposable> _subscriptions = [];
	private bool _disposed;

	public string ModuleId => moduleId;

	public async Task<PluginUser?> GetUserAsync() =>
		await currentUser.GetAsync() is { } user ? new PluginUser(user.UserName, user.IsAdmin, user.IsModule) : null;

	public async Task<PrinterSnapshot> GetPrinterAsync(CancellationToken ct = default)
	{
		PrinterStatusDto status = await mediator.Send(new GetPrinterStatusQuery(), ct);
		JobSnapshot? job = status.Job is { } j
			? new JobSnapshot(j.FileName, j.Progress, j.Status is JobStatus.Printing or JobStatus.Paused, j.Status == JobStatus.Paused, j.Kind == JobKind.SdPrint)
			: null;
		return new PrinterSnapshot(Activity(status.State), job, status.Heaters.Select(h => new HeaterSnapshot(h.Heater, h.Actual, h.Target)).ToList());
	}

	private static PrinterActivity Activity(PrinterState state) => state switch
	{
		PrinterState.Offline => PrinterActivity.Offline,
		PrinterState.Connecting => PrinterActivity.Connecting,
		PrinterState.Operational => PrinterActivity.Idle,
		PrinterState.Printing => PrinterActivity.Printing,
		PrinterState.Paused => PrinterActivity.Paused,
		PrinterState.Cancelling => PrinterActivity.Cancelling,
		PrinterState.Transferring => PrinterActivity.Transferring,
		_ => PrinterActivity.Error
	};

	public IDisposable Subscribe(Action<PrinterEvent> handler)
	{
		IDisposable subscription = stream.Subscribe(handler);
		lock (_gate)
		{
			if (_disposed)
			{
				subscription.Dispose();
				return subscription;
			}
			_subscriptions.Add(subscription);
		}
		return subscription;
	}

	public Task<PluginResult> SendGcodeAsync(string gcode, CancellationToken ct = default) => Send(new SendGcodeCommand(gcode), ct);
	public Task<PluginResult> StartPrintAsync(string fileName, CancellationToken ct = default) => Send(new StartPrintCommand(fileName), ct);
	public Task<PluginResult> PauseAsync(CancellationToken ct = default) => Send(new PauseJobCommand(), ct);
	public Task<PluginResult> ResumeAsync(CancellationToken ct = default) => Send(new ResumeJobCommand(), ct);
	public Task<PluginResult> CancelAsync(CancellationToken ct = default) => Send(new CancelJobCommand(), ct);
	public Task<PluginResult> LogAsync(string message, CancellationToken ct = default) => Send(new AddTerminalNoteCommand(message), ct);

	private async Task<PluginResult> Send(ICommand<Result> command, CancellationToken ct)
	{
		Result result = await mediator.Send(command, ct);
		return result.IsSuccess ? PluginResult.Success() : PluginResult.Failure(result.Error ?? "Something went wrong.");
	}

	public async Task<bool> IsEnabledAsync(CancellationToken ct = default) =>
		(await mediator.Send(new GetModuleSettingsQuery(moduleId), ct))?.Enabled ?? false;

	public async Task<PluginResult<T>> GetSettingsAsync<T>(CancellationToken ct = default) where T : class, new()
	{
		Result<JsonObject> settings = await mediator.Send(new GetModuleSecretSettingsQuery(moduleId), ct);
		return settings.IsSuccess
			? PluginResult.Success(ModuleSettingsJson.Read<T>(settings.Value))
			: PluginResult.Failure<T>(settings.Error ?? "Something went wrong.");
	}

	public async Task<EditableSettings<T>> GetEditableSettingsAsync<T>(CancellationToken ct = default) where T : class, new()
	{
		ModuleSettingsDto? settings = await mediator.Send(new GetModuleSettingsQuery(moduleId), ct);
		return new EditableSettings<T>(ModuleSettingsJson.Read<T>(settings?.Settings), settings?.SavedSecrets ?? new HashSet<string>());
	}

	public Task<PluginResult> SaveSettingsAsync<T>(T settings, CancellationToken ct = default) where T : class, new() =>
		Send(new SaveModuleSettingsCommand(moduleId, ModuleSettingsJson.Write(settings)), ct);

	public void Dispose()
	{
		List<IDisposable> subscriptions;
		lock (_gate)
		{
			_disposed = true;
			subscriptions = [.. _subscriptions];
			_subscriptions.Clear();
		}
		foreach (IDisposable s in subscriptions) s.Dispose();
	}
}
