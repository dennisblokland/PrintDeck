using System.Buffers.Binary;
using System.Runtime.InteropServices;

using Mediator;

using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Domain.Files;

namespace PrintDeck.Web.Api;

/// <summary>
/// Serves a file's toolpath to the viewer as a compact little-endian binary blob:
/// <code>
/// header   "PDTP" u32 version, u32 layers, u32 points, f32 minX minY maxX maxY, f64 totalBytes   (40 bytes)
/// layers   layers × (f32 z, u32 start, u32 count)
/// xy       points × 2 f32
/// offsets  points × u32   file byte offset at which the move is read
/// kinds    points × u8    ToolpathKind (0 = travel to this point)
/// </code>
/// Same-origin UI endpoint: needs the login cookie (or the API key), like the rest of the UI.
/// </summary>
public static class ToolpathApi
{
	private const uint Version = 1;

	public static void MapToolpathApi(this WebApplication app) =>
		app.MapGet("/toolpath/{name}", async (string name, HttpContext http, IMediator mediator, CancellationToken ct) =>
		{
			ToolpathResult? result = await mediator.Send(new GetGcodeToolpathQuery(name), ct);
			if (result is null) return Results.NotFound();

			string etag = $"\"{result.File.UploadedAt.UtcTicks:x}-{result.File.SizeBytes:x}-{Version}\"";
			http.Response.Headers.CacheControl = "no-cache";
			http.Response.Headers.ETag = etag;
			if (http.Request.Headers.IfNoneMatch == etag) return Results.StatusCode(StatusCodes.Status304NotModified);

			return Results.Bytes(Encode(result.Toolpath), "application/octet-stream");
		});

	internal static byte[] Encode(GcodeToolpath t)
	{
		int layers = t.Layers.Count, points = t.PointCount;
		int size = 40 + layers * 12 + points * 8 + points * 4 + points;
		byte[] buf = new byte[size];
		Span<byte> s = buf;

		"PDTP"u8.CopyTo(s);
		BinaryPrimitives.WriteUInt32LittleEndian(s[4..], Version);
		BinaryPrimitives.WriteInt32LittleEndian(s[8..], layers);
		BinaryPrimitives.WriteInt32LittleEndian(s[12..], points);
		BinaryPrimitives.WriteSingleLittleEndian(s[16..], t.Bounds.MinX);
		BinaryPrimitives.WriteSingleLittleEndian(s[20..], t.Bounds.MinY);
		BinaryPrimitives.WriteSingleLittleEndian(s[24..], t.Bounds.MaxX);
		BinaryPrimitives.WriteSingleLittleEndian(s[28..], t.Bounds.MaxY);
		BinaryPrimitives.WriteDoubleLittleEndian(s[32..], t.TotalBytes);

		int p = 40;
		foreach (ToolpathLayer l in t.Layers)
		{
			BinaryPrimitives.WriteSingleLittleEndian(s[p..], l.Z);
			BinaryPrimitives.WriteInt32LittleEndian(s[(p + 4)..], l.Start);
			BinaryPrimitives.WriteInt32LittleEndian(s[(p + 8)..], l.Count);
			p += 12;
		}

		if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Big-endian hosts are not supported.");
		MemoryMarshal.AsBytes(t.Xy.AsSpan(0, points * 2)).CopyTo(s[p..]);
		p += points * 8;
		MemoryMarshal.AsBytes(t.Offsets.AsSpan(0, points)).CopyTo(s[p..]);
		p += points * 4;
		t.Kinds.AsSpan(0, points).CopyTo(s[p..]);
		return buf;
	}
}
