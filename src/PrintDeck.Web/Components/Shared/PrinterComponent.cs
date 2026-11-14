using Mediator;

using Microsoft.AspNetCore.Components;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Web.Services;
using PrintDeck.Web.Services.Enums;

namespace PrintDeck.Web.Components.Shared;

/// <summary>
/// Base for live components: reads go through queries, writes through commands,
/// and printer events trigger a throttled re-query.
/// </summary>
public abstract class PrinterComponent : ComponentBase, IDisposable
{
	[Inject] protected IMediator Mediator { get; set; } = default!;
	[Inject] protected IPrinterEvents Events { get; set; } = default!;
	[Inject] protected ToastService Toasts { get; set; } = default!;
	[Inject] protected TimeProvider Time { get; set; } = default!;

	private int _scheduled;
	private bool _disposed;

	protected virtual PrinterChange Interests => PrinterChange.All;
	protected virtual TimeSpan Throttle => TimeSpan.FromMilliseconds(200);

	protected abstract Task RefreshAsync();

	protected override async Task OnInitializedAsync()
	{
		Events.Changed += OnPrinterChanged;
		await RefreshAsync();
	}

	private void OnPrinterChanged(PrinterChange change)
	{
		if ((change & Interests) == 0 || Interlocked.Exchange(ref _scheduled, 1) == 1) return;
		_ = Task.Run(async () =>
		{
			await Task.Delay(Throttle);
			Interlocked.Exchange(ref _scheduled, 0);
			await ReloadAsync();
		});
	}

	protected async Task ReloadAsync()
	{
		if (_disposed) return;
		try
		{
			await InvokeAsync(async () =>
			{
				await RefreshAsync();
				StateHasChanged();
			});
		}
		catch (ObjectDisposedException) { }
	}

	/// <summary>Sends a command and shows its error as a toast.</summary>
	protected async Task<bool> Run(ICommand<Result> command, string? success = null)
	{
		Result result = await Mediator.Send(command);
		if (!result.IsSuccess) Toasts.Error(result.Error ?? "Something went wrong.");
		else if (success is not null) Toasts.Success(success);
		return result.IsSuccess;
	}

	public virtual void Dispose()
	{
		_disposed = true;
		Events.Changed -= OnPrinterChanged;
		GC.SuppressFinalize(this);
	}
}
