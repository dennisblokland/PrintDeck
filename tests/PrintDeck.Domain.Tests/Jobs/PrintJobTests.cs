using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Domain.Tests.Jobs;

public class PrintJobTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

	private static PrintJob NewJob(long totalBytes = 1000, TimeSpan? estimate = null) => new("a.gcode", totalBytes, estimate, _t0);

	public class Construction
	{
		[Fact]
		public void Starts_printing_with_no_progress()
		{
			PrintJob job = NewJob();
			Assert.Equal(JobStatus.Printing, job.Status);
			Assert.True(job.IsActive);
			Assert.Equal(0, job.Progress);
			Assert.Equal(_t0, job.StartedAt);
			Assert.Null(job.EndedAt);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		public void Rejects_an_empty_file(long totalBytes)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => NewJob(totalBytes));
		}
	}

	public class Progress
	{
		[Fact]
		public void Advance_counts_bytes_and_lines()
		{
			PrintJob job = NewJob();
			job.Advance(250);
			job.Advance(250);
			Assert.Equal(500, job.BytesProcessed);
			Assert.Equal(2, job.LinesSent);
			Assert.Equal(0.5, job.Progress);
		}

		[Fact]
		public void Advance_never_passes_the_file_size()
		{
			PrintJob job = NewJob();
			job.Advance(600);
			job.Advance(600);
			Assert.Equal(1000, job.BytesProcessed);
			Assert.Equal(1, job.Progress);
		}

		[Fact]
		public void Report_position_sets_absolute_progress_within_the_file()
		{
			PrintJob job = new("cube.gcode", 1000, null, _t0, JobKind.SdPrint);
			job.ReportPosition(400);
			Assert.Equal(0.4, job.Progress);
			job.ReportPosition(250);
			Assert.Equal(250, job.BytesProcessed);
			job.ReportPosition(5000);
			Assert.Equal(1, job.Progress);
			Assert.Equal(0, job.LinesSent);
			Assert.Equal(JobKind.SdPrint, job.Kind);
		}

		[Fact]
		public void Kind_defaults_to_a_streamed_print() => Assert.Equal(JobKind.Print, NewJob().Kind);

		[Fact]
		public void Complete_sets_full_progress()
		{
			PrintJob job = NewJob(10);
			job.Complete(_t0.AddSeconds(5));
			Assert.Equal(1, job.Progress);
			Assert.False(job.IsActive);
			Assert.Equal(JobStatus.Completed, job.Status);
		}

		[Fact]
		public void Cancel_keeps_partial_progress()
		{
			PrintJob job = NewJob();
			job.Advance(300);
			job.Cancel(_t0.AddMinutes(1));
			Assert.Equal(0.3, job.Progress);
			Assert.Equal(JobStatus.Cancelled, job.Status);
		}
	}

	public class PauseAndResume
	{
		[Fact]
		public void Cannot_resume_a_running_job()
		{
			Assert.Throws<InvalidOperationException>(() => NewJob().Resume(_t0));
		}

		[Fact]
		public void Cannot_pause_twice()
		{
			PrintJob job = NewJob();
			job.Pause(_t0);
			Assert.Throws<InvalidOperationException>(() => job.Pause(_t0));
		}

		[Fact]
		public void Cannot_pause_a_finished_job()
		{
			PrintJob job = NewJob();
			job.Complete(_t0);
			Assert.Throws<InvalidOperationException>(() => job.Pause(_t0));
		}

		[Fact]
		public void Pause_and_resume_switch_status()
		{
			PrintJob job = NewJob();
			job.Pause(_t0);
			Assert.Equal(JobStatus.Paused, job.Status);
			Assert.True(job.IsActive);

			job.Resume(_t0.AddMinutes(1));
			Assert.Equal(JobStatus.Printing, job.Status);
		}
	}

	public class Timing
	{
		[Fact]
		public void Paused_time_is_not_counted_as_elapsed()
		{
			PrintJob job = NewJob();
			job.Pause(_t0.AddMinutes(10));
			job.Resume(_t0.AddMinutes(25));
			Assert.Equal(TimeSpan.FromMinutes(15), job.Elapsed(_t0.AddMinutes(30)));
		}

		[Fact]
		public void Elapsed_stands_still_while_paused()
		{
			PrintJob job = NewJob();
			job.Pause(_t0.AddMinutes(10));
			Assert.Equal(TimeSpan.FromMinutes(10), job.Elapsed(_t0.AddMinutes(50)));
		}

		[Fact]
		public void Elapsed_is_frozen_once_the_job_ends()
		{
			PrintJob job = NewJob();
			job.Complete(_t0.AddMinutes(5));
			Assert.Equal(_t0.AddMinutes(5), job.EndedAt);
			Assert.Equal(TimeSpan.FromMinutes(5), job.Elapsed(_t0.AddHours(1)));
		}

		[Fact]
		public void Cancelling_while_paused_excludes_the_pause()
		{
			PrintJob job = NewJob();
			job.Pause(_t0.AddMinutes(10));
			job.Cancel(_t0.AddMinutes(20));
			Assert.Equal(TimeSpan.FromMinutes(10), job.Elapsed(_t0.AddHours(1)));
		}

		[Fact]
		public void Ending_twice_keeps_the_first_outcome()
		{
			PrintJob job = NewJob();
			job.Cancel(_t0.AddMinutes(1));
			job.Complete(_t0.AddMinutes(2));
			job.Fail(_t0.AddMinutes(3));
			Assert.Equal(JobStatus.Cancelled, job.Status);
			Assert.Equal(_t0.AddMinutes(1), job.EndedAt);
		}

		[Fact]
		public void Fail_ends_the_job()
		{
			PrintJob job = NewJob();
			job.Fail(_t0.AddMinutes(1));
			Assert.Equal(JobStatus.Failed, job.Status);
			Assert.False(job.IsActive);
		}
	}

	public class EstimatedRemaining
	{
		[Fact]
		public void Is_the_slicer_estimate_at_the_start()
		{
			PrintJob job = NewJob(estimate: TimeSpan.FromHours(1));
			Assert.Equal(TimeSpan.FromHours(1), job.EstimatedRemaining(_t0));
		}

		[Fact]
		public void Blends_slicer_estimate_with_actual_rate()
		{
			PrintJob job = NewJob(estimate: TimeSpan.FromHours(1));
			job.Advance(499); // ~50 %
			TimeSpan remaining = job.EstimatedRemaining(_t0.AddMinutes(40))!.Value;
			Assert.InRange(remaining.TotalMinutes, 30, 40); // between slicer (30) and linear (40)
		}

		[Fact]
		public void Extrapolates_linearly_without_a_slicer_estimate()
		{
			PrintJob job = NewJob();
			job.Advance(500);
			Assert.Equal(TimeSpan.FromMinutes(10), job.EstimatedRemaining(_t0.AddMinutes(10)));
		}

		[Fact]
		public void Is_unknown_without_estimate_or_progress()
		{
			Assert.Null(NewJob().EstimatedRemaining(_t0.AddMinutes(10)));
		}

		[Theory]
		[InlineData(JobStatus.Completed)]
		[InlineData(JobStatus.Cancelled)]
		[InlineData(JobStatus.Failed)]
		public void Is_zero_once_the_job_ends(JobStatus status)
		{
			PrintJob job = NewJob(estimate: TimeSpan.FromHours(1));
			switch (status)
			{
				case JobStatus.Completed: job.Complete(_t0); break;
				case JobStatus.Cancelled: job.Cancel(_t0); break;
				default: job.Fail(_t0); break;
			}
			Assert.Equal(TimeSpan.Zero, job.EstimatedRemaining(_t0.AddMinutes(1)));
		}
	}
}
