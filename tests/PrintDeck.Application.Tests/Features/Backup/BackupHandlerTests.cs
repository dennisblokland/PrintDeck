using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Backup.Commands;
using PrintDeck.Application.Features.Backup.Dtos;
using PrintDeck.Application.Features.Backup.Handlers;
using PrintDeck.Application.Features.Backup.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Backup;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer.Enums;

namespace PrintDeck.Application.Tests.Features.Backup;

public class BackupHandlerTests
{
	private readonly FakePrinterSession _session = new() { State = PrinterState.Operational };
	private readonly FakeSettingsStore _settings = new();
	private readonly FakeSerialLog _serialLog = new();
	private readonly FakeBackupArchive _archive;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public BackupHandlerTests() => _archive = new FakeBackupArchive(_settings);

	private async Task<(Result<BackupRestore> Result, List<PrinterChange> Published)> Restore()
	{
		(PrinterEventHub hub, List<PrinterChange> published) = RecordingEventHub.Create();
		RestoreBackupHandler handler = new(_archive, _session, _settings, _serialLog, new ToolpathCache(), hub, NullLogger<RestoreBackupHandler>.Instance);
		Result<BackupRestore> result = await handler.Handle(new RestoreBackupCommand(new MemoryStream()), Ct);
		return (result, published);
	}

	[Fact]
	public async Task Download_is_named_after_the_time_and_streams_the_archive()
	{
		ManualTimeProvider time = new(new DateTimeOffset(2026, 11, 17, 15, 30, 0, TimeSpan.Zero));

		BackupDownload download = await new GetBackupHandler(_archive, time).Handle(new GetBackupQuery(), Ct);
		MemoryStream body = new();
		await download.WriteAsync(body, Ct);

		Assert.Equal("printdeck-backup-20261117-1530.zip", download.FileName);
		Assert.Equal("zip"u8.ToArray(), body.ToArray());
	}

	[Fact]
	public async Task Restores_and_tells_the_pages()
	{
		(Result<BackupRestore> result, List<PrinterChange> published) = await Restore();

		Assert.True(result.IsSuccess);
		Assert.Equal(["cube.gcode"], result.Value!.Files);
		Assert.Equal([PrinterChange.Settings | PrinterChange.Files], published);
	}

	[Fact]
	public async Task Follows_the_restored_serial_log_switch()
	{
		await Restore();
		Assert.True(_serialLog.Enabled);
	}

	[Theory]
	[InlineData(PrinterState.Printing)]
	[InlineData(PrinterState.Paused)]
	[InlineData(PrinterState.Transferring)]
	public async Task Refuses_while_the_printer_is_busy(PrinterState state)
	{
		_session.State = state;

		(Result<BackupRestore> result, List<PrinterChange> published) = await Restore();

		Assert.Equal("Finish or cancel the print first.", result.Error);
		Assert.Equal(0, _archive.Restored);
		Assert.Empty(published);
	}

	[Fact]
	public async Task Passes_on_a_refused_archive_and_changes_nothing()
	{
		_archive.Outcome = Result.Failure<BackupRestore>("That file is not a PrintDeck backup.");

		(Result<BackupRestore> result, List<PrinterChange> published) = await Restore();

		Assert.Equal("That file is not a PrintDeck backup.", result.Error);
		Assert.Empty(published);
		Assert.False(_serialLog.Enabled);
	}
}
