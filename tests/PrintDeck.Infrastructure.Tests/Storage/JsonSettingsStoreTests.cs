using Microsoft.Extensions.Options;

using PrintDeck.Domain.Settings;
using PrintDeck.Infrastructure.Storage;
using PrintDeck.Infrastructure.Tests.TestSupport;

namespace PrintDeck.Infrastructure.Tests.Storage;

public sealed class JsonSettingsStoreTests : IDisposable
{
	private readonly TempDirectory _dir = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public void Dispose() => _dir.Dispose();

	private StorageOptions Storage => new() { DataDirectory = Path.Combine(_dir.Path, "data") };
	private JsonSettingsStore NewStore() => new(Options.Create(Storage));

	private async Task WriteFileAsync(string json)
	{
		Directory.CreateDirectory(Storage.DataDirectory);
		await File.WriteAllTextAsync(Storage.SettingsFile, json, Ct);
	}

	[Fact]
	public async Task First_start_writes_defaults_with_a_fresh_api_key()
	{
		HostSettings settings = await NewStore().GetAsync(Ct);

		Assert.Equal(new PrinterProfile().Name, settings.ActiveProfile.Name);
		Assert.Matches("^[0-9A-F]{32}$", settings.ApiKey);
		Assert.True(File.Exists(Storage.SettingsFile));
	}

	[Fact]
	public async Task The_api_key_survives_a_restart()
	{
		string key = (await NewStore().GetAsync(Ct)).ApiKey;
		Assert.Equal(key, (await NewStore().GetAsync(Ct)).ApiKey);
	}

	[Fact]
	public async Task Saved_settings_round_trip()
	{
		PrinterProfile mk3s = new() { Id = "mk3s", Name = "MK3S", BedWidth = 250, BedDepth = 210 };
		PrinterProfile ender = new() { Id = "ender", Name = "Ender 3", BedWidth = 235, ExtruderCount = 2, HasHeatedBed = false };
		HostSettings saved = new()
		{
			Profiles = [mk3s, ender],
			ActiveProfileId = "ender",
			LastPort = "/dev/ttyUSB0",
			WebcamStreamUrl = "http://cam/stream",
			ApiKey = "ABC",
			Presets = [new TemperaturePreset("TPU", 225, 50)]
		};
		await NewStore().UpdateAsync(_ => saved, Ct);

		HostSettings loaded = await NewStore().GetAsync(Ct);

		Assert.Equal(saved with { Presets = [], Profiles = [], Snippets = [] }, loaded with { Presets = [], Profiles = [], Snippets = [] });
		Assert.Equal(saved.Presets, loaded.Presets);
		Assert.Equal(saved.Snippets, loaded.Snippets);
		Assert.Equal(saved.Profiles, loaded.Profiles);
		Assert.Equal(ender, loaded.ActiveProfile);
	}

	[Fact]
	public async Task Update_applies_the_change_to_the_latest_settings()
	{
		JsonSettingsStore store = NewStore();
		await store.UpdateAsync(s => s with { LastPort = "/dev/ttyACM0" }, Ct);
		await store.UpdateAsync(s => s with { DefaultBaudRate = 250000 }, Ct);

		HostSettings loaded = await NewStore().GetAsync(Ct);
		Assert.Equal(("/dev/ttyACM0", 250000), (loaded.LastPort, loaded.DefaultBaudRate));
	}

	[Fact]
	public async Task Update_repairs_the_result()
	{
		HostSettings s = await NewStore().UpdateAsync(s => s with { Profiles = [], ActiveProfileId = "gone" }, Ct);

		Assert.Single(s.Profiles);
		Assert.Equal(s.Profiles[0].Id, s.ActiveProfileId);
	}

	[Fact]
	public async Task Get_returns_the_saved_settings_without_rereading()
	{
		JsonSettingsStore store = NewStore();
		HostSettings saved = await store.UpdateAsync(s => s with { ApiKey = "K" }, Ct);
		File.Delete(Storage.SettingsFile);

		Assert.Same(saved, await store.GetAsync(Ct));
	}

	[Fact]
	public async Task The_shared_key_is_on_unless_switched_off()
	{
		await WriteFileAsync("""{ "profiles": [{ "id": "a", "name": "Old" }], "activeProfileId": "a", "apiKey": "K" }""");
		Assert.True((await NewStore().GetAsync(Ct)).SharedApiKeyEnabled);

		await NewStore().UpdateAsync(s => s with { SharedApiKeyEnabled = false }, Ct);

		Assert.False((await NewStore().GetAsync(Ct)).SharedApiKeyEnabled);
	}

	[Fact]
	public async Task Adds_an_api_key_to_an_older_settings_file()
	{
		await WriteFileAsync("""{ "profiles": [{ "id": "a", "name": "Old" }], "activeProfileId": "a", "apiKey": "" }""");

		HostSettings settings = await NewStore().GetAsync(Ct);

		Assert.Equal("Old", settings.ActiveProfile.Name);
		Assert.NotEmpty(settings.ApiKey);
		Assert.Contains(settings.ApiKey, await File.ReadAllTextAsync(Storage.SettingsFile, Ct));
	}

	[Fact]
	public async Task Turns_a_single_profile_file_into_the_default_profile()
	{
		await WriteFileAsync("""
			{
			  "name": "Prusa MK3S", "bedWidth": 250, "bedDepth": 210, "maxHeight": 210, "extruderCount": 1, "hasHeatedBed": true,
			  "defaultBaudRate": 115200, "lastPort": "/dev/ttyACM0", "jogFeedrateXY": 4800, "jogFeedrateZ": 500, "extrudeFeedrate": 250,
			  "webcamStreamUrl": "http://cam/stream", "apiKey": "OLDKEY", "cancelGcode": "M84",
			  "presets": [{ "name": "PLA", "hotend": 215, "bed": 60 }]
			}
			""");

		HostSettings settings = await NewStore().GetAsync(Ct);

		PrinterProfile expected = new()
		{
			Id = PrinterProfile.DefaultId, Name = "Prusa MK3S", BedWidth = 250, BedDepth = 210, MaxHeight = 210,
			JogFeedrateXY = 4800, JogFeedrateZ = 500, ExtrudeFeedrate = 250
		};
		Assert.Equal([expected], settings.Profiles);
		Assert.Equal(expected, settings.ActiveProfile);
		Assert.Equal(("/dev/ttyACM0", 115200, "http://cam/stream", "OLDKEY", "M84"),
			(settings.LastPort, settings.DefaultBaudRate, settings.WebcamStreamUrl, settings.ApiKey, settings.CancelGcode));
		// A file from before G-code scripts gets the default snippets and empty scripts.
		Assert.Equal(["disable_hotends", "disable_bed"], settings.Snippets.Select(s => s.Name));
		Assert.Equal("", settings.BeforePrintGcode);
		Assert.Equal([new TemperaturePreset("PLA", 215, 60)], settings.Presets);

		string rewritten = await File.ReadAllTextAsync(Storage.SettingsFile, Ct);
		Assert.Contains("\"profiles\"", rewritten);
		Assert.Equal(settings.Profiles, (await NewStore().GetAsync(Ct)).Profiles);
	}
}
