using PrintDeck.Domain.Common;

namespace PrintDeck.Domain.Tests.Common;

public class RingBufferTests
{
	private static RingBuffer<int> Filled(int capacity, params int[] items)
	{
		RingBuffer<int> buffer = new(capacity);
		foreach (int i in items) buffer.Add(i);
		return buffer;
	}

	[Fact]
	public void Keeps_items_in_insertion_order_below_capacity()
	{
		Assert.Equal([1, 2], Filled(3, 1, 2).Snapshot());
	}

	[Fact]
	public void Drops_the_oldest_item_when_full()
	{
		Assert.Equal([3, 4, 5], Filled(3, 1, 2, 3, 4, 5).Snapshot());
	}

	[Theory]
	[InlineData(0, new int[0])]
	[InlineData(2, new[] { 4, 5 })]
	[InlineData(3, new[] { 3, 4, 5 })]
	[InlineData(10, new[] { 3, 4, 5 })]
	public void Snapshot_returns_the_last_n_items(int last, int[] expected)
	{
		Assert.Equal(expected, Filled(3, 1, 2, 3, 4, 5).Snapshot(last));
	}

	[Fact]
	public void Clear_empties_and_the_buffer_stays_usable()
	{
		RingBuffer<int> buffer = Filled(3, 1, 2, 3, 4);
		buffer.Clear();
		Assert.Empty(buffer.Snapshot());

		buffer.Add(7);
		Assert.Equal([7], buffer.Snapshot());
	}

	[Fact]
	public void Exposes_capacity()
	{
		Assert.Equal(5, new RingBuffer<string>(5).Capacity);
	}
}
