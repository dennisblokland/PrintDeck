using System.Buffers.Binary;
using System.Text;

using PrintDeck.Domain.Files;
using PrintDeck.Web.Api;

namespace PrintDeck.Web.Tests.Api;

/// <summary>The binary layout the viewer's <c>toolpath.ts</c> parses.</summary>
public class ToolpathApiEncodeTests
{
	private static readonly GcodeToolpath _toolpath = new(
		layers: [new ToolpathLayer(0.2f, 0, 2), new ToolpathLayer(0.4f, 2, 1)],
		xy: [0, 0, 10, 0, 10, 10],
		offsets: [5, 10, 15],
		kinds: [(byte) ToolpathKind.Travel, (byte) ToolpathKind.OuterWall, (byte) ToolpathKind.Infill],
		bounds: (-1, -2, 10, 10),
		totalBytes: 20);

	private readonly byte[] _blob = ToolpathApi.Encode(_toolpath);

	private float F32(int at) => BinaryPrimitives.ReadSingleLittleEndian(_blob.AsSpan(at));
	private uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(_blob.AsSpan(at));

	[Fact]
	public void Has_the_expected_size()
	{
		// header 40 + layers 2 × 12 + xy 3 × 8 + offsets 3 × 4 + kinds 3
		Assert.Equal(40 + 24 + 24 + 12 + 3, _blob.Length);
	}

	[Fact]
	public void Writes_the_header()
	{
		Assert.Equal("PDTP", Encoding.ASCII.GetString(_blob, 0, 4));
		Assert.Equal(1u, U32(4));
		Assert.Equal(2u, U32(8));
		Assert.Equal(3u, U32(12));
		Assert.Equal((-1f, -2f, 10f, 10f), (F32(16), F32(20), F32(24), F32(28)));
		Assert.Equal(20d, BinaryPrimitives.ReadDoubleLittleEndian(_blob.AsSpan(32)));
	}

	[Fact]
	public void Writes_the_layer_table()
	{
		Assert.Equal((0.2f, 0u, 2u), (F32(40), U32(44), U32(48)));
		Assert.Equal((0.4f, 2u, 1u), (F32(52), U32(56), U32(60)));
	}

	[Fact]
	public void Writes_points_offsets_and_kinds()
	{
		Assert.Equal([0f, 0f, 10f, 0f, 10f, 10f], Enumerable.Range(0, 6).Select(i => F32(64 + i * 4)));
		Assert.Equal([5u, 10u, 15u], Enumerable.Range(0, 3).Select(i => U32(88 + i * 4)));
		Assert.Equal([0, 1, 3], _blob[100..]);
	}

	[Fact]
	public void Encodes_an_empty_toolpath_as_a_bare_header()
	{
		byte[] blob = ToolpathApi.Encode(new GcodeToolpath([], [], [], [], (0, 0, 0, 0), 0));
		Assert.Equal(40, blob.Length);
	}
}
