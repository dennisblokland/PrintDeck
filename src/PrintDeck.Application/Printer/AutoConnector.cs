using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Printer;

/// <summary>
/// With <see cref="HostSettings.AutoConnect"/> on, connects to the last used port and baud rate when PrintDeck starts,
/// and again each time that port appears (the printer is switched on or plugged back in).
/// A failed attempt is not repeated until the port disappears and comes back, because opening the port resets most boards.
/// </summary>
public sealed class AutoConnector(
	IPrinterSession session,
	ISettingsStore settings,
	IPortDiscovery ports,
	ILogger<AutoConnector> logger) : IDisposable
{
	public static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

	private readonly CancellationTokenSource _stop = new();

	/// <summary>The last port as it was present at the previous check; null when it was missing or auto-connect was off.</summary>
	private string? _presentPort;

	public void Start() => _ = Task.Run(() => RunAsync(_stop.Token));

	private async Task RunAsync(CancellationToken ct)
	{
		using PeriodicTimer timer = new(Interval);
		try
		{
			do
			{
				try { await CheckAsync(ct); }
				catch (Exception ex) when (!ct.IsCancellationRequested)
				{
					logger.LogWarning(ex, "Auto-connect check failed");
				}
			}
			while (await timer.WaitForNextTickAsync(ct));
		}
		catch (OperationCanceledException) { /* shutting down */ }
	}

	/// <summary>One check: connects if the last port has just appeared and the printer is offline. Returns whether it tried.</summary>
	public async Task<bool> CheckAsync(CancellationToken ct)
	{
		HostSettings s = await settings.GetAsync(ct);
		if (!s.AutoConnect || string.IsNullOrWhiteSpace(s.LastPort))
		{
			_presentPort = null;
			return false;
		}

		string port = s.LastPort;
		bool present = ports.ListPorts().Contains(port);
		bool appeared = present && _presentPort != port;
		_presentPort = present ? port : null;
		if (!appeared || session.State is not (PrinterState.Offline or PrinterState.Error)) return false;

		logger.LogInformation("Auto-connecting to {Port} @ {BaudRate}", port, s.DefaultBaudRate);
		Result result = await session.ConnectAsync(port, s.DefaultBaudRate, ct);
		if (!result.IsSuccess)
			logger.LogWarning("Auto-connect to {Port} failed: {Error}. Trying again when the port next appears.", port, result.Error);
		return true;
	}

	public void Dispose()
	{
		_stop.Cancel();
		_stop.Dispose();
	}
}
