using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Settings;

public class UpdateGcodeScriptsHandlerTests
{
	private readonly FakeSettingsStore _settings = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();

	private ValueTask<Result> Update(
		string before = "", string after = "", string cancel = "M84", string pause = "", string resume = "",
		params GcodeSnippet[] snippets) =>
		new UpdateGcodeScriptsHandler(_settings, _events.Hub).Handle(
			new UpdateGcodeScriptsCommand(before, after, cancel, pause, resume, snippets), TestContext.Current.CancellationToken);

	[Fact]
	public async Task Saves_every_script_and_the_snippets()
	{
		Result r = await Update(
			"M117 Starting\r\n", "{% snippet 'park' %}", "M104 S0\r\nM84", "G91\nG1 Z5\nG90", "M117 Back",
			new GcodeSnippet(" park ", "G1 X0 Y200\r\n"));

		Assert.True(r.IsSuccess, r.Error);
		HostSettings s = _settings.Settings;
		Assert.Equal(("M117 Starting", "{% snippet 'park' %}", "M104 S0\nM84", "G91\nG1 Z5\nG90", "M117 Back"),
			(s.BeforePrintGcode, s.AfterPrintGcode, s.CancelGcode, s.PauseGcode, s.ResumeGcode));
		Assert.Equal([new GcodeSnippet("park", "G1 X0 Y200")], s.Snippets);
		Assert.Contains(PrinterChange.Settings, _events.Published);
	}

	public static TheoryData<string, string> Invalid => new()
	{
		{ "bad name", "Snippet names need letters, digits, - or _ only." },
		{ "duplicate name", "Snippet names must be unique." },
		{ "unknown in cancel", "After cancel: Unknown snippet 'park'." },
		{ "unknown in resume", "On resume: Unknown snippet 'park'." },
		{ "unknown in snippet", "Snippet off: Unknown snippet 'heaters'." },
		{ "loop", "Before print: Snippet 'a' includes itself." },
	};

	private ValueTask<Result> UpdateInvalid(string reason) => reason switch
	{
		"bad name" => Update(snippets: new GcodeSnippet("park here", "G1 X0")),
		"duplicate name" => Update(snippets: [new("park", "G1 X0"), new("Park", "G1 X1")]),
		"unknown in cancel" => Update(cancel: "{% snippet 'park' %}"),
		"unknown in resume" => Update(resume: "{% snippet 'park' %}"),
		"unknown in snippet" => Update(snippets: new GcodeSnippet("off", "{% snippet 'heaters' %}")),
		"loop" => Update(before: "{% snippet 'a' %}", snippets: new GcodeSnippet("a", "{% snippet 'a' %}")),
		_ => throw new ArgumentOutOfRangeException(nameof(reason))
	};

	[Theory]
	[MemberData(nameof(Invalid))]
	public async Task Rejects_scripts_that_do_not_expand(string reason, string error)
	{
		Assert.Equal(error, (await UpdateInvalid(reason)).Error);
		Assert.Equal(0, _settings.Saves);
		Assert.Empty(_events.Published);
	}
}
