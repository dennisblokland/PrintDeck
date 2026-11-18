using System.IO.Compression;
using System.Text.Json;

using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Backup;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Infrastructure.Storage;

/// <summary>
/// Backups as zip files laid out by <see cref="BackupLayout"/>. A restore unpacks everything into a hidden staging
/// folder first, so a damaged archive is refused before any file or setting changes.
/// </summary>
public sealed class ZipBackupArchive(IOptions<StorageOptions> options, ISettingsStore settings, TimeProvider time) : IBackupArchive
{
	public const string NotABackup = "That file is not a PrintDeck backup.";
	public const string Damaged = "The backup is damaged.";
	public const string InvalidName = "The backup contains a file name PrintDeck can't use.";

	private const int _buffer = 81920;
	private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	private readonly StorageOptions _storage = options.Value;

	public async Task WriteAsync(Stream destination, CancellationToken ct)
	{
		string uploads = Directory.CreateDirectory(_storage.UploadsDirectory).FullName;
		List<string> names = Directory.EnumerateFiles(uploads, "*", SearchOption.AllDirectories)
			.Select(p => Path.GetRelativePath(uploads, p).Replace(Path.DirectorySeparatorChar, '/'))
			.Where(n => BackupLayout.UploadName(BackupLayout.UploadsPrefix + n) is not null)
			.Order(StringComparer.Ordinal)
			.ToList();
		HostSettings current = await settings.GetAsync(ct);
		BackupManifest manifest = BackupManifest.Create(time.GetUtcNow(), names.Count(n => BackupLayout.SidecarOwner(n) is null));

		await using ZipArchive zip = await ZipArchive.CreateAsync(destination, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, ct);
		await WriteJsonAsync(zip, BackupLayout.ManifestEntry, manifest, ct);
		await WriteJsonAsync(zip, BackupLayout.SettingsEntry, current, ct);
		foreach (string name in names)
		{
			FileInfo info = new(Path.Combine(uploads, name));
			FileStream source;
			try
			{
				source = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, _buffer,
					FileOptions.Asynchronous | FileOptions.SequentialScan);
			}
			catch (FileNotFoundException)
			{
				continue; // deleted since the listing
			}
			await using (source)
			{
				// G-code is text and squeezes well; Fastest keeps a Raspberry Pi from spending minutes on it.
				ZipArchiveEntry entry = zip.CreateEntry(BackupLayout.UploadsPrefix + name, CompressionLevel.Fastest);
				// Zip keeps local clock time without a zone; a restore reads it back as local time.
				entry.LastWriteTime = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero).ToLocalTime();
				await using Stream target = await entry.OpenAsync(ct);
				await source.CopyToAsync(target, ct);
			}
		}
	}

	public async Task<Result<BackupRestore>> RestoreAsync(Stream archive, CancellationToken ct)
	{
		// A zip is read from its end, so the upload goes to disk first. Not to /tmp: that is RAM on many Pis.
		Directory.CreateDirectory(_storage.DataDirectory);
		string tempPath = Path.Combine(_storage.DataDirectory, $"restore-{Guid.NewGuid():N}.zip.tmp");
		await using FileStream temp = new(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, _buffer,
			FileOptions.Asynchronous | FileOptions.DeleteOnClose);
		await archive.CopyToAsync(temp, ct);
		temp.Position = 0;

		ZipArchive zip;
		try
		{
			zip = await ZipArchive.CreateAsync(temp, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null, ct);
		}
		catch (InvalidDataException)
		{
			return Result.Failure<BackupRestore>(NotABackup);
		}

		await using (zip)
		{
			Result<BackupManifest> manifest = await ReadJsonAsync<BackupManifest>(zip, BackupLayout.ManifestEntry, NotABackup, ct);
			if (!manifest.IsSuccess) return Result.Failure<BackupRestore>(manifest.Error!);
			if (manifest.Value!.Problem() is { } problem) return Result.Failure<BackupRestore>(problem);

			Result<HostSettings> restored = await ReadJsonAsync<HostSettings>(zip, BackupLayout.SettingsEntry, Damaged, ct);
			if (!restored.IsSuccess) return Result.Failure<BackupRestore>(restored.Error!);

			Result<List<(string Name, ZipArchiveEntry Entry)>> uploads = UploadsIn(zip);
			if (!uploads.IsSuccess) return Result.Failure<BackupRestore>(uploads.Error!);

			string root = Directory.CreateDirectory(_storage.UploadsDirectory).FullName;
			// Inside uploads, so moving into place is a rename; hidden, so the store never lists what is in it.
			string staging = Directory.CreateDirectory(Path.Combine(root, $".restore-{Guid.NewGuid():N}")).FullName;
			try
			{
				foreach ((string name, ZipArchiveEntry entry) in uploads.Value!)
				{
					try
					{
						string staged = Path.Combine(staging, name);
						Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
						await ExtractAsync(entry, staged, ct);
					}
					catch (InvalidDataException)
					{
						return Result.Failure<BackupRestore>(Damaged);
					}
				}

				List<string> files = [];
				foreach ((string name, _) in uploads.Value!.Where(u => BackupLayout.SidecarOwner(u.Name) is null))
				{
					string path = PathIn(root, name);
					// The backup's sidecars (moved next) describe this file; the old ones describe whatever it replaces.
					foreach (string suffix in BackupLayout.SidecarSuffixes) File.Delete(path + suffix);
					File.Move(Path.Combine(staging, name), path, overwrite: true);
					files.Add(name);
				}
				foreach ((string name, _) in uploads.Value!.Where(u => BackupLayout.SidecarOwner(u.Name) is not null))
					File.Move(Path.Combine(staging, name), PathIn(root, name), overwrite: true);

				HostSettings backup = restored.Value!;
				await settings.UpdateAsync(now => string.IsNullOrEmpty(backup.ApiKey) ? backup with { ApiKey = now.ApiKey } : backup, ct);
				return Result.Success(new BackupRestore(manifest.Value!.CreatedAt, files));
			}
			finally
			{
				try { Directory.Delete(staging, recursive: true); } catch (IOException) { /* best effort */ }
			}
		}
	}

	/// <summary>The uploads to restore: every file, plus the sidecars of files that are in the archive too.</summary>
	private static Result<List<(string Name, ZipArchiveEntry Entry)>> UploadsIn(ZipArchive zip)
	{
		Dictionary<string, ZipArchiveEntry> found = new(StringComparer.Ordinal);
		foreach (ZipArchiveEntry entry in zip.Entries)
		{
			// Folder entries ("uploads/parts/") carry nothing; their files bring the folders along.
			if (!entry.FullName.StartsWith(BackupLayout.UploadsPrefix, StringComparison.Ordinal) || entry.FullName.EndsWith('/'))
				continue;
			if (BackupLayout.UploadName(entry.FullName) is not { } name)
				return Result.Failure<List<(string, ZipArchiveEntry)>>(InvalidName);
			found[name] = entry;
		}
		List<(string, ZipArchiveEntry)> uploads = found
			.Where(f => BackupLayout.SidecarOwner(f.Key) is not { } owner || found.ContainsKey(owner))
			.Select(f => (f.Key, f.Value))
			.ToList();
		return Result.Success(uploads);
	}

	/// <summary>Where an upload goes, its folder created; never outside the uploads folder.</summary>
	private static string PathIn(string root, string name)
	{
		string full = Path.GetFullPath(Path.Combine(root, name));
		if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
			throw new InvalidOperationException($"Refusing to write outside the uploads folder: {name}");
		Directory.CreateDirectory(Path.GetDirectoryName(full)!);
		return full;
	}

	private static async Task ExtractAsync(ZipArchiveEntry entry, string path, CancellationToken ct)
	{
		long written;
		await using (Stream source = await entry.OpenAsync(ct))
		await using (FileStream target = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, _buffer, FileOptions.Asynchronous))
		{
			await source.CopyToAsync(target, ct);
			written = target.Length;
		}
		// .NET checks the CRC only when the data comes out at the recorded length; a damaged stream can end short.
		if (written != entry.Length) throw new InvalidDataException($"{entry.FullName} is {written} bytes, not {entry.Length}.");
		// The store shows a file's write time as "uploaded"; keep the original one.
		File.SetLastWriteTimeUtc(path, entry.LastWriteTime.UtcDateTime);
	}

	private static async Task WriteJsonAsync<T>(ZipArchive zip, string name, T value, CancellationToken ct)
	{
		ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
		await using Stream s = await entry.OpenAsync(ct);
		await JsonSerializer.SerializeAsync(s, value, _json, ct);
	}

	private static async Task<Result<T>> ReadJsonAsync<T>(ZipArchive zip, string name, string error, CancellationToken ct) where T : class
	{
		if (zip.GetEntry(name) is not { } entry) return Result.Failure<T>(error);
		try
		{
			await using Stream s = await entry.OpenAsync(ct);
			return await JsonSerializer.DeserializeAsync<T>(s, _json, ct) is { } value ? Result.Success(value) : Result.Failure<T>(error);
		}
		catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
		{
			return Result.Failure<T>(error);
		}
	}
}
