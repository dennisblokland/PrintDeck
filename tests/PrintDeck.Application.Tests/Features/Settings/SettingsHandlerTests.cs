using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Settings;

public class SettingsHandlerTests
{
	private static readonly PrinterProfile _mk3s = new() { Id = "mk3s", Name = "MK3S", BedWidth = 250, BedDepth = 210, MaxHeight = 210 };
	private static readonly PrinterProfile _ender = new() { Id = "ender", Name = "Ender 3", BedWidth = 235, BedDepth = 235 };

	private readonly FakeSettingsStore _settings = new(new HostSettings { ApiKey = "SECRET", Profiles = [_mk3s, _ender], ActiveProfileId = "mk3s" });
	private readonly FakePrinterSession _session = new();
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result<string>> Save(PrinterProfile p) =>
		new SavePrinterProfileHandler(_settings, _events.Hub).Handle(new SavePrinterProfileCommand(p), Ct);

	private ValueTask<Result> Select(string id) =>
		new SelectPrinterProfileHandler(_session, _settings, _events.Hub).Handle(new SelectPrinterProfileCommand(id), Ct);

	private ValueTask<Result> Delete(string id) =>
		new DeletePrinterProfileHandler(_settings, _events.Hub).Handle(new DeletePrinterProfileCommand(id), Ct);

	private ValueTask<Result> UpdateHost(IReadOnlyList<TemperaturePreset>? presets = null, string? webcam = null) =>
		new UpdateSettingsHandler(_settings, _events.Hub).Handle(new UpdateSettingsCommand(presets ?? [new("PLA", 205, 60)], webcam), Ct);

	public class Profiles : SettingsHandlerTests
	{
		public static TheoryData<string, string> InvalidProfiles => new()
		{
			{ "empty name", "Name is required." },
			{ "blank name", "Name is required." },
			{ "long name", "Name can be at most 64 characters." },
			{ "zero bed width", "Build volume must be 1–2000 mm." },
			{ "huge bed depth", "Build volume must be 1–2000 mm." },
			{ "zero height", "Build volume must be 1–2000 mm." },
			{ "no extruders", "Extruder count must be 1–8." },
			{ "nine extruders", "Extruder count must be 1–8." },
			{ "zero jog feedrate", "Feedrates must be positive." },
			{ "negative extrude feedrate", "Feedrates must be positive." },
		};

		private static PrinterProfile Invalid(string reason) => reason switch
		{
			"empty name" => _mk3s with { Name = "" },
			"blank name" => _mk3s with { Name = "   " },
			"long name" => _mk3s with { Name = new string('x', 65) },
			"zero bed width" => _mk3s with { BedWidth = 0 },
			"huge bed depth" => _mk3s with { BedDepth = 2001 },
			"zero height" => _mk3s with { MaxHeight = 0 },
			"no extruders" => _mk3s with { ExtruderCount = 0 },
			"nine extruders" => _mk3s with { ExtruderCount = 9 },
			"zero jog feedrate" => _mk3s with { JogFeedrateXY = 0 },
			"negative extrude feedrate" => _mk3s with { ExtrudeFeedrate = -1 },
			_ => throw new ArgumentOutOfRangeException(nameof(reason))
		};

		[Theory]
		[MemberData(nameof(InvalidProfiles))]
		public async Task Save_rejects_invalid_profiles(string reason, string error)
		{
			Assert.Equal(error, (await Save(Invalid(reason))).Error);
			Assert.Equal(0, _settings.Saves);
			Assert.Empty(_events.Published);
		}

		[Fact]
		public async Task Save_replaces_an_existing_profile_and_announces_it()
		{
			PrinterProfile changed = _ender with { BedWidth = 220, Name = "  Ender 3 V2 " };

			Result<string> r = await Save(changed);

			Assert.True(r.IsSuccess);
			Assert.Equal("ender", r.Value);
			Assert.Equal(changed with { Name = "Ender 3 V2" }, _settings.Settings.FindProfile("ender"));
			Assert.Equal(2, _settings.Settings.Profiles.Count);
			Assert.Equal("mk3s", _settings.Settings.ActiveProfileId);
			Assert.Contains(PrinterChange.Settings, _events.Published);
		}

		[Fact]
		public async Task Save_without_an_id_adds_a_profile()
		{
			Result<string> r = await Save(new PrinterProfile { Id = "", Name = "Voron" });

			Assert.True(r.IsSuccess);
			Assert.NotEmpty(r.Value!);
			Assert.Equal(["MK3S", "Ender 3", "Voron"], _settings.Settings.Profiles.Select(p => p.Name));
			Assert.Equal("Voron", _settings.Settings.FindProfile(r.Value!)?.Name);
			Assert.Equal("mk3s", _settings.Settings.ActiveProfileId);
		}

		[Fact]
		public async Task Save_refuses_a_duplicate_name()
		{
			Result<string> r = await Save(new PrinterProfile { Id = "", Name = "ender 3" });

			Assert.Equal("A profile named ender 3 already exists.", r.Error);
			Assert.Equal(2, _settings.Settings.Profiles.Count);
			Assert.Empty(_events.Published);
		}

		[Fact]
		public async Task Save_refuses_a_profile_that_was_deleted()
		{
			Result<string> r = await Save(_ender with { Id = "gone" });

			Assert.Equal("That profile no longer exists.", r.Error);
			Assert.Null(_settings.Settings.FindProfile("gone"));
		}

		[Fact]
		public async Task Save_caps_the_number_of_profiles()
		{
			for (int i = _settings.Settings.Profiles.Count; i < SavePrinterProfileHandler.MaxProfiles; i++)
				Assert.True((await Save(new PrinterProfile { Id = "", Name = $"P{i}" })).IsSuccess);

			Assert.Equal($"You can keep up to {SavePrinterProfileHandler.MaxProfiles} profiles.", (await Save(new PrinterProfile { Id = "", Name = "One more" })).Error);
		}

		[Fact]
		public async Task Select_switches_the_active_profile()
		{
			Assert.True((await Select("ender")).IsSuccess);
			Assert.Equal("ender", _settings.Settings.ActiveProfileId);
			Assert.Contains(PrinterChange.Settings, _events.Published);
		}

		[Theory]
		[InlineData(PrinterState.Printing)]
		[InlineData(PrinterState.Paused)]
		[InlineData(PrinterState.Cancelling)]
		public async Task Select_waits_for_the_print_to_end(PrinterState state)
		{
			_session.State = state;

			Assert.Equal("Finish or cancel the print before switching printer profiles.", (await Select("ender")).Error);
			Assert.Equal("mk3s", _settings.Settings.ActiveProfileId);
		}

		[Fact]
		public async Task Select_of_the_active_profile_is_a_no_op_even_while_printing()
		{
			_session.State = PrinterState.Printing;
			Assert.True((await Select("mk3s")).IsSuccess);
			Assert.Equal(0, _settings.Saves);
		}

		[Fact]
		public async Task Select_rejects_an_unknown_profile()
		{
			Assert.Equal("That profile no longer exists.", (await Select("nope")).Error);
			Assert.Equal("mk3s", _settings.Settings.ActiveProfileId);
			Assert.Empty(_events.Published);
		}

		[Fact]
		public async Task Delete_removes_an_inactive_profile()
		{
			Assert.True((await Delete("ender")).IsSuccess);
			Assert.Equal(["mk3s"], _settings.Settings.Profiles.Select(p => p.Id));
			Assert.Contains(PrinterChange.Settings, _events.Published);
		}

		[Fact]
		public async Task Delete_keeps_the_active_profile()
		{
			Assert.Equal("Switch to another profile before deleting this one.", (await Delete("mk3s")).Error);
			Assert.Equal(2, _settings.Settings.Profiles.Count);
		}

		[Fact]
		public async Task Delete_rejects_an_unknown_profile()
		{
			Assert.Equal("That profile no longer exists.", (await Delete("nope")).Error);
			Assert.Empty(_events.Published);
		}

		[Fact]
		public async Task Get_profile_returns_the_active_one()
		{
			await Select("ender");
			Assert.Equal(_ender, await new GetPrinterProfileHandler(_settings).Handle(new GetPrinterProfileQuery(), Ct));
		}
	}

	public class Host : SettingsHandlerTests
	{
		[Fact]
		public async Task Saves_the_printer_screen_switch()
		{
			Assert.True(_settings.Settings.ShowProgressOnPrinter); // on by default

			await new UpdateSettingsHandler(_settings, _events.Hub).Handle(
				new UpdateSettingsCommand([new("PLA", 205, 60)], null, ShowProgressOnPrinter: false), Ct);

			Assert.False(_settings.Settings.ShowProgressOnPrinter);
		}

		[Fact]
		public async Task Saves_the_auto_connect_switch()
		{
			Assert.False(_settings.Settings.AutoConnect); // off by default

			await new UpdateSettingsHandler(_settings, _events.Hub).Handle(
				new UpdateSettingsCommand([new("PLA", 205, 60)], null, AutoConnect: true), Ct);

			Assert.True(_settings.Settings.AutoConnect);
		}

		public static TheoryData<string, string> InvalidSettings => new()
		{
			{ "nameless preset", "Presets need a name, hotend 0–320 °C and bed 0–130 °C." },
			{ "hot preset", "Presets need a name, hotend 0–320 °C and bed 0–130 °C." },
			{ "hot bed preset", "Presets need a name, hotend 0–320 °C and bed 0–130 °C." },
			{ "duplicate preset", "Preset names must be unique." },
			{ "relative webcam url", "Webcam URL is not a valid URL." },
		};

		private ValueTask<Result> UpdateInvalid(string reason) => reason switch
		{
			"nameless preset" => UpdateHost(presets: [new(" ", 200, 60)]),
			"hot preset" => UpdateHost(presets: [new("PC", 321, 60)]),
			"hot bed preset" => UpdateHost(presets: [new("PC", 300, 131)]),
			"duplicate preset" => UpdateHost(presets: [new("PLA", 200, 60), new("pla", 210, 60)]),
			"relative webcam url" => UpdateHost(webcam: "camera/stream"),
			_ => throw new ArgumentOutOfRangeException(nameof(reason))
		};

		[Theory]
		[MemberData(nameof(InvalidSettings))]
		public async Task Update_rejects_invalid_settings(string reason, string error)
		{
			Assert.Equal(error, (await UpdateInvalid(reason)).Error);
			Assert.Equal(0, _settings.Saves);
			Assert.Empty(_events.Published);
		}

		[Fact]
		public async Task Update_saves_host_settings_and_leaves_profiles_and_key_alone()
		{
			HostSettings before = _settings.Settings;

			Assert.True((await UpdateHost([new(" TPU ", 225, 50)], " http://cam.local/stream ")).IsSuccess);

			HostSettings s = _settings.Settings;
			Assert.Equal([new TemperaturePreset("TPU", 225, 50)], s.Presets);
			Assert.Equal(before.CancelGcode, s.CancelGcode);
			Assert.Equal("http://cam.local/stream", s.WebcamStreamUrl);
			Assert.Equal("SECRET", s.ApiKey);
			Assert.Equal(before.Profiles, s.Profiles);
			Assert.Equal(before.ActiveProfileId, s.ActiveProfileId);
			Assert.Contains(PrinterChange.Settings, _events.Published);
		}

		[Fact]
		public async Task Blank_webcam_url_clears_it()
		{
			await UpdateHost(webcam: "http://cam/stream");
			await UpdateHost(webcam: "  ");
			Assert.Null(_settings.Settings.WebcamStreamUrl);
		}

		[Fact]
		public async Task Get_returns_the_stored_settings()
		{
			Assert.Same(_settings.Settings, await new GetSettingsHandler(_settings).Handle(new GetSettingsQuery(), Ct));
		}

		[Fact]
		public async Task Regenerate_stores_a_new_random_key()
		{
			Result<string> r = await new RegenerateApiKeyHandler(_settings, _events.Hub).Handle(new RegenerateApiKeyCommand(), Ct);

			Assert.True(r.IsSuccess);
			Assert.Matches("^[0-9A-F]{32}$", r.Value);
			Assert.NotEqual("SECRET", r.Value);
			Assert.Equal(r.Value, _settings.Settings.ApiKey);
			Assert.Contains(PrinterChange.Settings, _events.Published);
		}

		[Fact]
		public void New_keys_are_unique()
		{
			Assert.NotEqual(RegenerateApiKeyHandler.NewKey(), RegenerateApiKeyHandler.NewKey());
		}
	}
}
