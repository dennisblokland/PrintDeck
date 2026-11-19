using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Control.Commands;
using PrintDeck.Application.Features.Control.Handlers;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Control;

public class CustomButtonHandlerTests
{
	private readonly FakeSettingsStore _settings = new(new HostSettings { ApiKey = "KEY", Snippets = [new("park", "G1 X0 Y200")] });
	private readonly FakePrinterSession _session = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Save(params CustomButton[] buttons) =>
		new SaveCustomButtonsHandler(_settings, _events.Hub).Handle(new SaveCustomButtonsCommand(buttons), Ct);

	private ValueTask<Result> Press(int index, string label) =>
		new RunCustomButtonHandler(_session, _settings).Handle(new RunCustomButtonCommand(index, label), Ct);

	[Fact]
	public async Task Saves_the_buttons_in_order_tidied_up()
	{
		Result r = await Save(
			new CustomButton(" Level ", "G28 W\r\nG80\r\n", "scan", Confirm: true),
			new CustomButton("Change", "M600", " ", IdleOnly: false));

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal(
			[new CustomButton("Level", "G28 W\nG80", "scan", Confirm: true), new CustomButton("Change", "M600", null, IdleOnly: false)],
			_settings.Settings.CustomButtons);
		Assert.Contains(PrinterChange.Settings, _events.Published);
	}

	[Fact]
	public async Task Refuses_a_bad_button_and_keeps_the_old_ones()
	{
		await Save(new CustomButton("Off", "M84"));

		Assert.Equal("Stop: M112 is an emergency stop: the printer halts mid-print.", (await Save(new CustomButton("Stop", "M112"))).Error);
		Assert.Equal("Park: Unknown snippet 'nope'.", (await Save(new CustomButton("Park", "{% snippet 'nope' %}"))).Error);
		Assert.Equal([new CustomButton("Off", "M84")], _settings.Settings.CustomButtons);
	}

	[Fact]
	public async Task Refuses_too_many_buttons()
	{
		CustomButton[] many = Enumerable.Range(0, CustomButton.MaxButtons + 1).Select(i => new CustomButton($"B{i}", "M84")).ToArray();
		Assert.Equal("At most 40 buttons.", (await Save(many)).Error);
	}

	[Fact]
	public async Task Saving_no_buttons_removes_them()
	{
		await Save(new CustomButton("Off", "M84"));
		Assert.True((await Save()).IsSuccess);
		Assert.Empty(_settings.Settings.CustomButtons);
	}

	[Fact]
	public async Task Pressing_sends_the_lines_with_snippets()
	{
		await Save(new CustomButton("Off", "M84"), new CustomButton("Park", "G90\n{% snippet 'park' %}"));

		Assert.True((await Press(1, "Park")).IsSuccess);
		Assert.Equal(["G90", "G1 X0 Y200"], _session.Enqueued);
	}

	[Theory]
	[InlineData(PrinterState.Operational, true)]
	[InlineData(PrinterState.Paused, true)]
	[InlineData(PrinterState.Printing, false)]
	[InlineData(PrinterState.Transferring, false)]
	public async Task Idle_only_buttons_follow_the_jogging_rule(PrinterState state, bool sent)
	{
		await Save(new CustomButton("Level", "G80"));
		_session.State = state;

		Result r = await Press(0, "Level");

		Assert.Equal(sent, r.IsSuccess);
		Assert.Equal(sent ? ["G80"] : [], _session.Enqueued);
	}

	[Fact]
	public async Task Other_buttons_work_while_printing_but_not_offline()
	{
		await Save(new CustomButton("Change", "M600", IdleOnly: false));

		_session.State = PrinterState.Printing;
		Assert.True((await Press(0, "Change")).IsSuccess);

		_session.State = PrinterState.Offline;
		Assert.Equal("Printer is not connected.", (await Press(0, "Change")).Error);
		Assert.Equal(["M600"], _session.Enqueued);
	}

	[Theory]
	[InlineData(1, "Off")]
	[InlineData(-1, "Off")]
	[InlineData(0, "Park")]
	public async Task A_page_with_an_old_list_sends_nothing(int index, string label)
	{
		await Save(new CustomButton("Off", "M84"));

		Assert.Equal(RunCustomButtonHandler.Changed, (await Press(index, label)).Error);
		Assert.Empty(_session.Enqueued);
	}

	[Fact]
	public async Task Removing_a_snippet_a_button_uses_is_refused()
	{
		await Save(new CustomButton("Park", "{% snippet 'park' %}"));

		Result r = await new UpdateGcodeScriptsHandler(_settings, _events.Hub).Handle(
			new UpdateGcodeScriptsCommand("", "", "", "", "", []), Ct);

		Assert.Equal("Button Park: Unknown snippet 'park'.", r.Error);
		Assert.Single(_settings.Settings.Snippets);
	}
}
