using PrintDeck.Domain.Camera;

namespace PrintDeck.Domain.Tests.Camera;

public class SnapshotSourceTests
{
	[Theory]
	[InlineData("http://cam.local/?action=snapshot")]
	[InlineData(" https://cam.local/snap.jpg ")]
	public void Web_addresses_are_fetched(string url)
	{
		Assert.Equal(new Uri(url.Trim()), SnapshotSource.Parse(url));
	}

	[Theory]
	[InlineData("VIRTUAL")]
	[InlineData("virtual")]
	public void Virtual_is_the_pretend_camera(string url)
	{
		Assert.Equal(SnapshotSource.VirtualUri, SnapshotSource.Parse(url));
	}

	[Theory]
	[InlineData("file:///etc/passwd")]
	[InlineData("ftp://cam.local/snap.jpg")]
	[InlineData("virtual:camera")]
	[InlineData("camera/snapshot")]
	[InlineData("")]
	[InlineData(null)]
	public void Anything_else_is_refused(string? url)
	{
		Assert.Null(SnapshotSource.Parse(url));
	}
}
