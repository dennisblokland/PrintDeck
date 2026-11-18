using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Application.Features.Terminal.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Terminal;

namespace PrintDeck.Application.Tests.Features.Terminal;

public class UpdateTerminalFiltersHandlerTests
{
	private readonly FakeSettingsStore _settings = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();

	private ValueTask<Result> Update(params TerminalFilter[] filters) =>
		new UpdateTerminalFiltersHandler(_settings, _events.Hub).Handle(new UpdateTerminalFiltersCommand(filters), TestContext.Current.CancellationToken);

	[Fact]
	public async Task Saves_the_filters_trimmed()
	{
		Result r = await Update(new TerminalFilter(" Echo ", " ^echo: "), new TerminalFilter("Busy", "busy:", Enabled: false));

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal([new TerminalFilter("Echo", "^echo:"), new TerminalFilter("Busy", "busy:", Enabled: false)], _settings.Settings.TerminalFilters);
		Assert.Contains(PrinterChange.Settings, _events.Published);
	}

	[Fact]
	public async Task Saves_an_empty_list()
	{
		Assert.True((await Update()).IsSuccess);
		Assert.Empty(_settings.Settings.TerminalFilters);
	}

	public static TheoryData<string, string> Invalid => new()
	{
		{ "no name", "Every filter needs a name." },
		{ "long name", "Filter names can be at most 64 characters." },
		{ "duplicate", "Filter names must be unique." },
		{ "no pattern", "Echo: enter a pattern." },
		{ "long pattern", "Echo: the pattern can be at most 500 characters." },
		{ "bad pattern", "Echo: the pattern is not a valid regular expression." },
	};

	private ValueTask<Result> UpdateInvalid(string reason) => reason switch
	{
		"no name" => Update(new TerminalFilter("  ", "ok")),
		"long name" => Update(new TerminalFilter(new string('a', 65), "ok")),
		"duplicate" => Update(new TerminalFilter("Echo", "^echo:"), new TerminalFilter("echo", "echo")),
		"no pattern" => Update(new TerminalFilter("Echo", " ")),
		"long pattern" => Update(new TerminalFilter("Echo", new string('a', 501))),
		"bad pattern" => Update(new TerminalFilter("Echo", "(echo")),
		_ => throw new ArgumentOutOfRangeException(nameof(reason))
	};

	[Theory]
	[MemberData(nameof(Invalid))]
	public async Task Rejects_invalid_filters(string reason, string error)
	{
		Assert.Equal(error, (await UpdateInvalid(reason)).Error);
		Assert.Equal(0, _settings.Saves);
		Assert.Empty(_events.Published);
	}
}
