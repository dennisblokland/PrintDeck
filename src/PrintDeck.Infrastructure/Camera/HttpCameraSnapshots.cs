using System.Net.Http.Headers;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Camera;
using PrintDeck.Domain.Common;

namespace PrintDeck.Infrastructure.Camera;

/// <summary>Gets snapshots over HTTP, with a short timeout and a size limit.</summary>
public sealed class HttpCameraSnapshots(HttpMessageHandler handler) : ICameraSnapshots, IDisposable
{
	public const int MaxBytes = 20 << 20;
	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	private readonly HttpClient _http = new(handler) { Timeout = Timeout };

	public HttpCameraSnapshots() : this(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { }

	public async Task<Result<CameraSnapshot>> TakeAsync(Uri url, CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
			if (!response.IsSuccessStatusCode)
				return Result.Failure<CameraSnapshot>($"The camera answered with HTTP {(int)response.StatusCode}.");

			MediaTypeHeaderValue? type = response.Content.Headers.ContentType;
			if (type?.MediaType is not { } mediaType || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
				return Result.Failure<CameraSnapshot>("The snapshot URL did not return an image.");
			if (response.Content.Headers.ContentLength > MaxBytes)
				return Result.Failure<CameraSnapshot>("The snapshot is too large.");

			await using Stream body = await response.Content.ReadAsStreamAsync(ct);
			using MemoryStream buffer = new();
			byte[] chunk = new byte[81920];
			int read;
			while ((read = await body.ReadAsync(chunk, ct)) > 0)
			{
				if (buffer.Length + read > MaxBytes) return Result.Failure<CameraSnapshot>("The snapshot is too large.");
				buffer.Write(chunk, 0, read);
			}
			if (buffer.Length == 0) return Result.Failure<CameraSnapshot>("The snapshot URL did not return an image.");
			return Result.Success(new CameraSnapshot(buffer.ToArray(), mediaType.ToLowerInvariant()));
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
		{
			return Result.Failure<CameraSnapshot>("The camera did not answer.");
		}
	}

	public void Dispose() => _http.Dispose();
}
