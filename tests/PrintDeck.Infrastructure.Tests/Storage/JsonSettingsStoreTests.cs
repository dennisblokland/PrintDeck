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

	[Fact]
	public async Task First_start_writes_defaults_with_a_fresh_api_key()
	{
		PrinterProfile profile = await NewStore().GetAsync(Ct);

		Assert.Equal(new PrinterProfile().Name, profile.Name);
		Assert.Matches("^[0-9A-F]{32}$", profile.ApiKey);
		Assert.True(File.Exists(Storage.SettingsFile));
	}

	[Fact]
	public async Task The_api_key_survives_a_restart()
	{
		string key = (await NewStore().GetAsync(Ct)).ApiKey;
		Assert.Equal(key, (await NewStore().GetAsync(Ct)).ApiKey);
	}

	[Fact]
	public async Task Saved_profiles_round_trip()
	{
		PrinterProfile saved = new()
		{
			Name = "Ender 3",
			BedWidth = 235,
			ExtruderCount = 2,
			LastPort = "/dev/ttyUSB0",
			WebcamStreamUrl = "http://cam/stream",
			ApiKey = "ABC",
			Presets = [new TemperaturePreset("TPU", 225, 50)]
		};
		await NewStore().SaveAsync(saved, Ct);

		PrinterProfile loaded = await NewStore().GetAsync(Ct);

		Assert.Equal(saved with { Presets = [] }, loaded with { Presets = [] });
		Assert.Equal(saved.Presets, loaded.Presets);
	}

	[Fact]
	public async Task Get_returns_the_saved_profile_without_rereading()
	{
		JsonSettingsStore store = NewStore();
		PrinterProfile saved = new() { Name = "Cached", ApiKey = "K" };
		await store.SaveAsync(saved, Ct);
		File.Delete(Storage.SettingsFile);

		Assert.Same(saved, await store.GetAsync(Ct));
	}

	[Fact]
	public async Task Adds_an_api_key_to_an_older_settings_file()
	{
		Directory.CreateDirectory(Storage.DataDirectory);
		await File.WriteAllTextAsync(Storage.SettingsFile, """{ "name": "Old", "apiKey": "" }""", Ct);

		PrinterProfile profile = await NewStore().GetAsync(Ct);

		Assert.Equal("Old", profile.Name);
		Assert.NotEmpty(profile.ApiKey);
		Assert.Contains(profile.ApiKey, await File.ReadAllTextAsync(Storage.SettingsFile, Ct));
	}
}
