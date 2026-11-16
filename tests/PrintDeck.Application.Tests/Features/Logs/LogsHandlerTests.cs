using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Logs.Commands;
using PrintDeck.Application.Features.Logs.Dtos;
using PrintDeck.Application.Features.Logs.Handlers;
using PrintDeck.Application.Features.Logs.Queries;
using PrintDeck.Application.Tests.TestDoubles;

namespace PrintDeck.Application.Tests.Features.Logs;

public class LogsHandlerTests
{
	private readonly FakeLogFiles _files = new FakeLogFiles().With("printdeck.log", "hello\n").With("serial.log", "> M105\n");
	private readonly FakeSerialLog _serialLog = new();
	private readonly FakeSettingsStore _settings = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Lists_the_files_and_the_serial_log_switch()
	{
		_serialLog.SetEnabled(true);

		LogsDto dto = await new GetLogsHandler(_files, _serialLog).Handle(new GetLogsQuery(), Ct);

		Assert.True(dto.SerialLogEnabled);
		Assert.Equal(["printdeck.log", "serial.log"], dto.Files.Select(f => f.Name).Order());
	}

	[Fact]
	public async Task Opens_a_listed_file_and_nothing_else()
	{
		OpenLogFileHandler handler = new(_files);

		await using Stream? stream = await handler.Handle(new OpenLogFileQuery("serial.log"), Ct);
		Assert.Equal("> M105\n", await new StreamReader(stream!).ReadToEndAsync(Ct));
		Assert.Null(await handler.Handle(new OpenLogFileQuery("../settings.json"), Ct));
	}

	[Fact]
	public async Task Switching_the_serial_log_is_remembered()
	{
		(Application.Abstractions.PrinterEventHub hub, List<PrinterChange> published) = RecordingEventHub.Create();
		SetSerialLogHandler handler = new(_settings, _serialLog, hub);

		Assert.True((await handler.Handle(new SetSerialLogCommand(true), Ct)).IsSuccess);
		Assert.True(_serialLog.Enabled);
		Assert.True(_settings.Settings.SerialLogEnabled);
		Assert.Contains(PrinterChange.Settings, published);

		await handler.Handle(new SetSerialLogCommand(false), Ct);
		Assert.False(_serialLog.Enabled);
		Assert.False(_settings.Settings.SerialLogEnabled);
	}
}
