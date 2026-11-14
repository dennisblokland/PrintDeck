using System.Text.Json;

using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Infrastructure.Storage;

public sealed class JsonSettingsStore(IOptions<StorageOptions> options) : ISettingsStore
{
	private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	private readonly SemaphoreSlim _lock = new(1, 1);
	private readonly string _path = options.Value.SettingsFile;
	private PrinterProfile? _cached;

	public async Task<PrinterProfile> GetAsync(CancellationToken ct)
	{
		if (_cached is not null) return _cached;
		await _lock.WaitAsync(ct);
		try
		{
			if (_cached is not null) return _cached;
			PrinterProfile? profile = null;
			if (File.Exists(_path))
			{
				await using FileStream s = File.OpenRead(_path);
				profile = await JsonSerializer.DeserializeAsync<PrinterProfile>(s, _json, ct);
			}
			profile ??= new PrinterProfile();
			if (string.IsNullOrEmpty(profile.ApiKey))
			{
				profile = profile with { ApiKey = RegenerateApiKeyHandler.NewKey() };
				await WriteAsync(profile, ct);
			}
			return _cached = profile;
		}
		finally { _lock.Release(); }
	}

	public async Task SaveAsync(PrinterProfile profile, CancellationToken ct)
	{
		await _lock.WaitAsync(ct);
		try
		{
			await WriteAsync(profile, ct);
			_cached = profile;
		}
		finally { _lock.Release(); }
	}

	private async Task WriteAsync(PrinterProfile profile, CancellationToken ct)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
		string temp = _path + ".tmp";
		await using (FileStream s = File.Create(temp))
			await JsonSerializer.SerializeAsync(s, profile, _json, ct);
		File.Move(temp, _path, overwrite: true);
	}
}
