using PrintDeck.Application.Features.SdCard.Commands;
using PrintDeck.Application.Features.SdCard.Dtos;
using PrintDeck.Application.Features.SdCard.Handlers;
using PrintDeck.Application.Features.SdCard.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.SdCard;
using PrintDeck.Domain.SdCard.Enums;

namespace PrintDeck.Application.Tests.Features.SdCard;

public class SdCardHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private readonly InMemoryGcodeFileStore _files = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Query_returns_the_card_state_and_files()
	{
		_session.SdCard = SdCardState.Ready;
		_session.SdFiles = [new SdFile("CUBE.GCO", 10, "cube.gcode")];

		SdCardDto dto = await new GetSdCardHandler(_session).Handle(new GetSdCardQuery(), Ct);

		Assert.Equal(SdCardState.Ready, dto.State);
		Assert.Equal("cube.gcode", Assert.Single(dto.Files).DisplayName);
	}

	[Fact]
	public async Task Commands_go_to_the_session()
	{
		await new RefreshSdCardHandler(_session).Handle(new RefreshSdCardCommand(), Ct);
		await new InitSdCardHandler(_session).Handle(new InitSdCardCommand(), Ct);
		await new ReleaseSdCardHandler(_session).Handle(new ReleaseSdCardCommand(), Ct);
		await new StartSdPrintHandler(_session).Handle(new StartSdPrintCommand("CUBE.GCO"), Ct);
		await new DeleteSdFileHandler(_session).Handle(new DeleteSdFileCommand("CUBE.GCO"), Ct);

		Assert.Equal(["refresh", "init", "release", "print CUBE.GCO", "delete CUBE.GCO"], _session.SdCalls);
	}

	[Fact]
	public async Task Session_refusals_come_back()
	{
		_session.JobResult = Result.Failure("No SD card.");
		Assert.Equal("No SD card.", (await new StartSdPrintHandler(_session).Handle(new StartSdPrintCommand("CUBE.GCO"), Ct)).Error);
	}

	[Fact]
	public async Task Copy_sends_the_stored_file_to_the_card()
	{
		_files.Add("cube.gcode", "G28\n");

		Result<string> result = await new CopyToSdCardHandler(_session, _files).Handle(new CopyToSdCardCommand("cube.gcode"), Ct);

		Assert.True(result.IsSuccess, result.Error);
		Assert.Equal("FILE.GCO", result.Value);
		Assert.Equal(("cube.gcode", 4L), _session.SdUpload);
	}

	[Fact]
	public async Task Copy_fails_for_missing_or_empty_files()
	{
		_files.Add("empty.gcode", "");
		CopyToSdCardHandler handler = new(_session, _files);

		Assert.Equal("File nope.gcode not found.", (await handler.Handle(new CopyToSdCardCommand("nope.gcode"), Ct)).Error);
		Assert.Equal("File is empty.", (await handler.Handle(new CopyToSdCardCommand("empty.gcode"), Ct)).Error);
		Assert.Null(_session.SdUpload);
	}
}
