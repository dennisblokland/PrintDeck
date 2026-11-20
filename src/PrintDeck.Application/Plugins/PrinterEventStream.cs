using System.Collections.Immutable;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Timelapse.Dtos;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Application.Timelapse;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Plugins;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Application.Plugins;

/// <summary>
/// Turns <see cref="PrinterEventHub"/>'s "something changed" into events modules can act on: connected, print started,
/// done, failed, paused, timelapse started… Derived once here by comparing with what was seen last, so every module
/// gets the same events in the same order, each once.
/// </summary>
public sealed class PrinterEventStream(IPrinterSession session, PrinterEventHub hub, TimelapseRecorder timelapse, TimeProvider time)
	: IDisposable
{
	private readonly Lock _gate = new();
	private readonly Lock _delivery = new();
	private ImmutableArray<Action<PrinterEvent>> _subscribers = [];
	private PrinterState _state;
	private PrintJob? _job;
	private JobStatus _jobStatus;
	private bool _timelapse;
	private bool _started;

	public void Start()
	{
		lock (_gate)
		{
			if (_started) return;
			_started = true;
			_state = session.State;
			_job = Followed(session.CurrentJob);
			_jobStatus = _job?.Status ?? JobStatus.Printing;
			_timelapse = IsRecording(timelapse.Status);
		}
		hub.Changed += OnChanged;
	}

	/// <summary>Calls <paramref name="handler"/> for each event until the subscription is disposed. Exceptions are swallowed.</summary>
	public IDisposable Subscribe(Action<PrinterEvent> handler)
	{
		ImmutableInterlocked.Update(ref _subscribers, s => s.Add(handler));
		return new Subscription(() => ImmutableInterlocked.Update(ref _subscribers, s => s.Remove(handler)));
	}

	private sealed class Subscription(Action dispose) : IDisposable
	{
		private Action? _dispose = dispose;

		public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
	}

	private static PrintJob? Followed(PrintJob? job) => job is { Kind: JobKind.Print or JobKind.SdPrint } ? job : null;

	private static bool IsRecording(TimelapseStatus status) => status.Recording is not null || status.Rendering is not null;

	private static bool IsConnected(PrinterState state) => state is not (PrinterState.Offline or PrinterState.Connecting or PrinterState.Error);

	private void OnChanged(PrinterChange change)
	{
		List<PrinterEvent> raised = [];
		DateTimeOffset now = time.GetUtcNow();
		lock (_delivery)
		{
			lock (_gate)
			{
				if ((change & (PrinterChange.State | PrinterChange.Job)) != 0) FollowState(raised, now);
				if ((change & (PrinterChange.State | PrinterChange.Job)) != 0) FollowJob(raised, now);
				if ((change & PrinterChange.Timelapse) != 0) FollowTimelapse(raised, now);
				if ((change & PrinterChange.Settings) != 0) raised.Add(new PrinterEvent(PrinterEventKind.SettingsChanged, now));
			}
			// Delivered in order, one change at a time, but outside _gate: a subscriber may publish a change itself.
			foreach (PrinterEvent e in raised) Deliver(e);
		}
	}

	private void FollowState(List<PrinterEvent> raised, DateTimeOffset now)
	{
		PrinterState state = session.State;
		PrinterState before = _state;
		if (state == before) return;
		_state = state;

		if (IsConnected(state) && !IsConnected(before)) raised.Add(new PrinterEvent(PrinterEventKind.Connected, now));
		else if (state == PrinterState.Offline) raised.Add(new PrinterEvent(PrinterEventKind.Disconnected, now));
		else if (state == PrinterState.Error) raised.Add(new PrinterEvent(PrinterEventKind.Error, now));
	}

	private void FollowJob(List<PrinterEvent> raised, DateTimeOffset now)
	{
		PrintJob? job = Followed(session.CurrentJob);
		if (job is null) return;

		if (!ReferenceEquals(job, _job))
		{
			// The last print may have ended unseen when a new one replaced it straight away.
			if (_job is { } previous && _jobStatus is JobStatus.Printing or JobStatus.Paused && Ended(previous.Status) is { } missed)
				raised.Add(new PrinterEvent(missed, now, previous.FileName));
			_job = job;
			_jobStatus = JobStatus.Printing;
			raised.Add(new PrinterEvent(PrinterEventKind.PrintStarted, now, job.FileName));
		}

		JobStatus status = job.Status;
		if (status == _jobStatus) return;
		JobStatus before = _jobStatus;
		_jobStatus = status;

		PrinterEventKind? kind = (before, status) switch
		{
			(JobStatus.Printing, JobStatus.Paused) => PrinterEventKind.PrintPaused,
			(JobStatus.Paused, JobStatus.Printing) => PrinterEventKind.PrintResumed,
			_ => Ended(status)
		};
		if (kind is { } k) raised.Add(new PrinterEvent(k, now, job.FileName));
	}

	private static PrinterEventKind? Ended(JobStatus status) => status switch
	{
		JobStatus.Completed => PrinterEventKind.PrintDone,
		JobStatus.Cancelled => PrinterEventKind.PrintCancelled,
		JobStatus.Failed => PrinterEventKind.PrintFailed,
		_ => null
	};

	private void FollowTimelapse(List<PrinterEvent> raised, DateTimeOffset now)
	{
		TimelapseStatus status = timelapse.Status;
		bool recording = IsRecording(status);
		if (recording == _timelapse) return;
		_timelapse = recording;
		raised.Add(new PrinterEvent(recording ? PrinterEventKind.TimelapseStarted : PrinterEventKind.TimelapseStopped, now,
			status.Recording ?? status.Rendering));
	}

	private void Deliver(PrinterEvent e)
	{
		foreach (Action<PrinterEvent> handler in _subscribers)
		{
			try { handler(e); }
			catch { /* a broken module must not break the printer loop */ }
		}
	}

	public void Dispose() => hub.Changed -= OnChanged;
}
