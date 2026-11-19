namespace PrintDeck.Domain.Camera;

/// <summary>
/// Where snapshots come from: an http or https URL that PrintDeck fetches, or <see cref="Virtual"/>,
/// a made-up camera picture for trying the camera features (and timelapses) without a webcam.
/// </summary>
public static class SnapshotSource
{
	public const string Virtual = "VIRTUAL";

	/// <summary>Stands for <see cref="Virtual"/> wherever a snapshot URL is passed on.</summary>
	public static readonly Uri VirtualUri = new("virtual:camera");

	public const string Invalid = "Snapshot URL must be an http or https URL.";

	/// <summary>The URL to fetch, or null when it is neither http(s) nor <see cref="Virtual"/> (no file: or other schemes).</summary>
	public static Uri? Parse(string? url)
	{
		string text = url?.Trim() ?? "";
		if (text.Equals(Virtual, StringComparison.OrdinalIgnoreCase)) return VirtualUri;
		return Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
			? uri
			: null;
	}
}
