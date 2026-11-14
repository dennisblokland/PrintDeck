using System.Text.Json;

using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Infrastructure.Storage;

public sealed class JsonSettingsStore(IOptions<StorageOptions> options) : ISettingsStore
{
	private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	private readonly SemaphoreSlim _lock = new(1, 1);
	private readonly string _path = options.Value.SettingsFile;
	private HostSettings? _cached;

	public async Task<HostSettings> GetAsync(CancellationToken ct)
	{
		if (_cached is not null) return _cached;
		await _lock.WaitAsync(ct);
		try { return await LoadAsync(ct); }
		finally { _lock.Release(); }
	}

	public async Task<HostSettings> UpdateAsync(Func<HostSettings, HostSettings> change, CancellationToken ct)
	{
		await _lock.WaitAsync(ct);
		try
		{
			HostSettings updated = change(await LoadAsync(ct)).Normalized();
			await WriteAsync(updated, ct);
			return _cached = updated;
		}
		finally { _lock.Release(); }
	}

	/// <summary>Call with the lock held.</summary>
	private async Task<HostSettings> LoadAsync(CancellationToken ct)
	{
		if (_cached is not null) return _cached;
		HostSettings? settings = null;
		bool rewrite = false;
		if (File.Exists(_path))
		{
			await using FileStream s = File.OpenRead(_path);
			using JsonDocument doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
			(settings, rewrite) = Read(doc.RootElement);
		}
		else rewrite = true;

		settings = (settings ?? new HostSettings()).Normalized();
		if (string.IsNullOrEmpty(settings.ApiKey))
		{
			settings = settings with { ApiKey = RegenerateApiKeyHandler.NewKey() };
			rewrite = true;
		}
		if (rewrite) await WriteAsync(settings, ct);
		return _cached = settings;
	}

	/// <summary>
	/// Before profiles, settings.json was one flat printer profile. Its host-wide fields keep their names, so the same
	/// object reads as both: the host settings and the one profile, which becomes the default.
	/// </summary>
	private static (HostSettings? Settings, bool Migrated) Read(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object) return (null, true);
		if (root.TryGetProperty("profiles", out _)) return (root.Deserialize<HostSettings>(_json), false);

		HostSettings host = root.Deserialize<HostSettings>(_json) ?? new HostSettings();
		PrinterProfile profile = (root.Deserialize<PrinterProfile>(_json) ?? new PrinterProfile()) with { Id = PrinterProfile.DefaultId };
		return (host with { Profiles = [profile], ActiveProfileId = profile.Id }, true);
	}

	private async Task WriteAsync(HostSettings settings, CancellationToken ct)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
		string temp = _path + ".tmp";
		await using (FileStream s = File.Create(temp))
			await JsonSerializer.SerializeAsync(s, settings, _json, ct);
		File.Move(temp, _path, overwrite: true);
	}
}
