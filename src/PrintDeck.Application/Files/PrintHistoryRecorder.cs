using Microsoft.Extensions.Logging;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Application.Files;

/// <summary>
/// Writes each finished print of a stored file to its history (completed, cancelled or failed),
/// so the file list can sort by last print and hide what already printed.
/// SD card prints and uploads are not stored files and are skipped.
/// </summary>
public sealed class PrintHistoryRecorder(
	IPrinterSession session,
	PrinterEventHub events,
	IGcodeFileStore files,
	ILogger<PrintHistoryRecorder> logger) : IDisposable
{
	private readonly Lock _gate = new();
	private PrintJob? _recorded;
	private Task _pending = Task.CompletedTask;

	public void Start() => events.Changed += OnChanged;

	/// <summary>Completes once the last record is written (for tests).</summary>
	public Task Pending
	{
		get { lock (_gate) return _pending; }
	}

	private void OnChanged(PrinterChange change)
	{
		if ((change & PrinterChange.Job) == 0) return;
		if (session.CurrentJob is not { Kind: JobKind.Print, IsActive: false, EndedAt: { } ended } job) return;

		// Read now: the job object is replaced when the next print starts.
		PrintRecord record = new(job.StartedAt, ended, job.Status, job.Elapsed(ended));
		double share = job.Status == JobStatus.Completed ? 1 : job.Progress;
		lock (_gate)
		{
			if (ReferenceEquals(job, _recorded)) return;
			_recorded = job;
			_pending = _pending.ContinueWith(_ => SaveAsync(job.FileName, record, share), TaskScheduler.Default).Unwrap();
		}
	}

	private async Task SaveAsync(string name, PrintRecord record, double share)
	{
		try
		{
			// Filament: the analysed total, or the printed share of it for a print that stopped early.
			if ((await files.GetAsync(name, CancellationToken.None))?.Analysis?.FilamentMillimeters is { } total)
				record = record with { FilamentMm = Math.Round(total * share, 1) };
			await files.RecordPrintAsync(name, record, CancellationToken.None);
			events.Publish(PrinterChange.Files); // the list shows "printed …" now
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not record the print of {File}", name);
		}
	}

	public void Dispose() => events.Changed -= OnChanged;
}
