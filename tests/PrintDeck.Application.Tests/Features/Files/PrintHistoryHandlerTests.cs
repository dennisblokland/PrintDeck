using PrintDeck.Application.Features.Files.Dtos;
using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Application.Tests.Features.Files;

public class PrintHistoryHandlerTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
	private readonly InMemoryGcodeFileStore _files = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task Printed(string name, int hour, JobStatus status, double filament)
	{
		if (!_files.Contains(name)) _files.Add(name, "G28\n");
		await _files.RecordPrintAsync(name,
			new PrintRecord(_t0.AddHours(hour), _t0.AddHours(hour + 1), status, TimeSpan.FromHours(1), filament), Ct);
	}

	[Fact]
	public async Task Lists_prints_of_all_files_newest_first_with_totals_over_everything()
	{
		await Printed("cube.gcode", 0, JobStatus.Completed, 1000);
		await Printed("benchy.gcode", 2, JobStatus.Failed, 300);
		await Printed("cube.gcode", 4, JobStatus.Completed, 1000);

		PrintHistoryDto history = await new GetPrintHistoryHandler(_files).Handle(new GetPrintHistoryQuery(Limit: 2), Ct);

		Assert.Equal([("cube.gcode", 4), ("benchy.gcode", 2)],
			history.Recent.Select(e => (e.FileName, (int) (e.Print.StartedAt - _t0).TotalHours)));
		Assert.Equal((3, 2, 2300.0), (history.Totals.Total, history.Totals.Completed, history.Totals.TotalFilamentMm));
		Assert.Equal(TimeSpan.FromHours(3), history.Totals.TotalPrintTime);
	}

	[Fact]
	public async Task Empty_without_prints()
	{
		PrintHistoryDto history = await new GetPrintHistoryHandler(_files).Handle(new GetPrintHistoryQuery(), Ct);
		Assert.Empty(history.Recent);
		Assert.Equal(0, history.Totals.Total);
	}
}
