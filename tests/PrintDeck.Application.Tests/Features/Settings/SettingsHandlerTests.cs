using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Settings;

public class SettingsHandlerTests
{
	private readonly FakeSettingsStore _settings = new(new PrinterProfile { ApiKey = "SECRET" });
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Update(PrinterProfile p) =>
		new UpdatePrinterProfileHandler(_settings, _events.Hub).Handle(new UpdatePrinterProfileCommand(p), Ct);

	public static TheoryData<string, string> InvalidProfiles => new()
	{
		{ "empty name", "Name is required." },
		{ "zero bed width", "Build volume must be 1–2000 mm." },
		{ "huge bed depth", "Build volume must be 1–2000 mm." },
		{ "zero height", "Build volume must be 1–2000 mm." },
		{ "no extruders", "Extruder count must be 1–8." },
		{ "nine extruders", "Extruder count must be 1–8." },
		{ "zero jog feedrate", "Feedrates must be positive." },
		{ "negative extrude feedrate", "Feedrates must be positive." },
		{ "nameless preset", "Presets need a name, hotend 0–320 °C and bed 0–130 °C." },
		{ "hot preset", "Presets need a name, hotend 0–320 °C and bed 0–130 °C." },
		{ "hot bed preset", "Presets need a name, hotend 0–320 °C and bed 0–130 °C." },
		{ "relative webcam url", "Webcam URL is not a valid URL." },
	};

	private static PrinterProfile Invalid(string reason)
	{
		PrinterProfile p = new();
		return reason switch
		{
			"empty name" => p with { Name = "" },
			"zero bed width" => p with { BedWidth = 0 },
			"huge bed depth" => p with { BedDepth = 2001 },
			"zero height" => p with { MaxHeight = 0 },
			"no extruders" => p with { ExtruderCount = 0 },
			"nine extruders" => p with { ExtruderCount = 9 },
			"zero jog feedrate" => p with { JogFeedrateXY = 0 },
			"negative extrude feedrate" => p with { ExtrudeFeedrate = -1 },
			"nameless preset" => p with { Presets = [new(" ", 200, 60)] },
			"hot preset" => p with { Presets = [new("PC", 321, 60)] },
			"hot bed preset" => p with { Presets = [new("PC", 300, 131)] },
			"relative webcam url" => p with { WebcamStreamUrl = "camera/stream" },
			_ => throw new ArgumentOutOfRangeException(nameof(reason))
		};
	}

	[Theory]
	[MemberData(nameof(InvalidProfiles))]
	public async Task Update_rejects_invalid_profiles(string reason, string error)
	{
		Assert.Equal(error, (await Update(Invalid(reason))).Error);
		Assert.Equal(0, _settings.Saves);
		Assert.Empty(_events.Published);
	}

	[Fact]
	public async Task Update_saves_and_announces_a_valid_profile()
	{
		PrinterProfile p = new() { Name = "Ender", BedWidth = 235, WebcamStreamUrl = "http://cam.local/stream", ApiKey = "SECRET" };

		Assert.True((await Update(p)).IsSuccess);
		Assert.Equal(p, _settings.Profile);
		Assert.Contains(PrinterChange.Settings, _events.Published);
	}

	[Fact]
	public async Task Update_never_changes_the_api_key()
	{
		await Update(new PrinterProfile { ApiKey = "FROM-THE-FORM" });
		Assert.Equal("SECRET", _settings.Profile.ApiKey);
	}

	[Fact]
	public async Task Get_returns_the_stored_profile()
	{
		PrinterProfile p = await new GetPrinterProfileHandler(_settings).Handle(new GetPrinterProfileQuery(), Ct);
		Assert.Same(_settings.Profile, p);
	}

	[Fact]
	public async Task Regenerate_stores_a_new_random_key()
	{
		Result<string> r = await new RegenerateApiKeyHandler(_settings, _events.Hub).Handle(new RegenerateApiKeyCommand(), Ct);

		Assert.True(r.IsSuccess);
		Assert.Matches("^[0-9A-F]{32}$", r.Value);
		Assert.NotEqual("SECRET", r.Value);
		Assert.Equal(r.Value, _settings.Profile.ApiKey);
		Assert.Contains(PrinterChange.Settings, _events.Published);
	}

	[Fact]
	public void New_keys_are_unique()
	{
		Assert.NotEqual(RegenerateApiKeyHandler.NewKey(), RegenerateApiKeyHandler.NewKey());
	}
}
