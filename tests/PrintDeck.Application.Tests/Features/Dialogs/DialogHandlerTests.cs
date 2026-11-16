using PrintDeck.Application.Features.Dialogs.Commands;
using PrintDeck.Application.Features.Dialogs.Dtos;
using PrintDeck.Application.Features.Dialogs.Handlers;
using PrintDeck.Application.Features.Dialogs.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer;

namespace PrintDeck.Application.Tests.Features.Dialogs;

public class DialogHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Query_returns_prompt_notification_and_wait()
	{
		_session.Prompt = new HostPrompt("Load filament", ["Continue"]);
		_session.Notification = new PrinterNotification(DateTimeOffset.UnixEpoch, "Runout");
		_session.WaitingForUser = true;

		PrinterDialogDto dto = await new GetPrinterDialogHandler(_session).Handle(new GetPrinterDialogQuery(), Ct);

		Assert.Equal(new PrinterDialogDto(_session.Prompt, _session.Notification, true), dto);
	}

	[Fact]
	public async Task Commands_go_to_the_session()
	{
		await new AnswerPromptHandler(_session).Handle(new AnswerPromptCommand(1), Ct);
		await new ContinueFromUserHandler(_session).Handle(new ContinueFromUserCommand(), Ct);
		await new DismissNotificationHandler(_session).Handle(new DismissNotificationCommand(), Ct);

		Assert.Equal(["answer 1", "continue", "dismiss"], _session.DialogCalls);
	}

	[Fact]
	public async Task Session_refusals_come_back()
	{
		_session.JobResult = Result.Failure("The printer is not asking anything.");
		Assert.Equal("The printer is not asking anything.", (await new AnswerPromptHandler(_session).Handle(new AnswerPromptCommand(0), Ct)).Error);
	}
}
