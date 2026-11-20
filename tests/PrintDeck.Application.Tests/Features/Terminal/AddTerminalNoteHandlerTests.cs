using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Application.Features.Terminal.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.Features.Terminal;

public sealed class AddTerminalNoteHandlerTests
{
	private readonly FakePrinterSession _session = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Note(string text) => new AddTerminalNoteHandler(_session).Handle(new AddTerminalNoteCommand(text), Ct);

	[Fact]
	public async Task Adds_one_info_line()
	{
		Assert.True((await Note(" Light on\nthrough Home Assistant ")).IsSuccess);

		Assert.Equal(["Light on through Home Assistant"], _session.Notes);
	}

	[Fact]
	public async Task Refuses_an_empty_note_and_cuts_a_long_one()
	{
		Assert.Equal("Nothing to write.", (await Note("  ")).Error);

		await Note(new string('x', 600));

		Assert.Equal(AddTerminalNoteHandler.MaxLength, Assert.Single(_session.Notes).Length);
	}
}
