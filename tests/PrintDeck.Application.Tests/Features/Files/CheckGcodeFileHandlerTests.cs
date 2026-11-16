using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Files.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Files;

public class CheckGcodeFileHandlerTests
{
	private readonly InMemoryGcodeFileStore _files = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<IReadOnlyList<GcodeProblem>?> Check(string name, PrinterProfile? profile = null) =>
		new CheckGcodeFileHandler(_files, profile is null ? new FakeSettingsStore() : new FakeSettingsStore(profile))
			.Handle(new CheckGcodeFileQuery(name), Ct);

	[Fact]
	public async Task Missing_files_give_null() => Assert.Null(await Check("nope.gcode"));

	[Fact]
	public async Task Checks_against_the_active_profile()
	{
		_files.Add("big.gcode", "M109 S215\nG28\nG1 X10 Y10\nG1 X300 Y10 E5\n");

		Assert.Empty((await Check("big.gcode", new PrinterProfile { BedWidth = 350, BedDepth = 350 }))!);
		Assert.Equal(GcodeProblemKind.OutsideBuildVolume,
			Assert.Single((await Check("big.gcode", new PrinterProfile { BedWidth = 250, BedDepth = 210 }))!).Kind);
	}
}
