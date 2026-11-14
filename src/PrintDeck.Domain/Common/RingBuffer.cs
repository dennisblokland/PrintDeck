namespace PrintDeck.Domain.Common;

/// <summary>Fixed-capacity, thread-safe FIFO that drops the oldest item when full.</summary>
public sealed class RingBuffer<T>(int capacity)
{
	private readonly T[] _items = new T[capacity];
	private readonly Lock _lock = new();
	private int _start;
	private int _count;

	public int Capacity => capacity;

	public void Add(T item)
	{
		lock (_lock)
		{
			int index = (_start + _count) % capacity;
			_items[index] = item;
			if (_count < capacity) _count++;
			else _start = (_start + 1) % capacity;
		}
	}

	public IReadOnlyList<T> Snapshot(int? last = null)
	{
		lock (_lock)
		{
			int take = Math.Min(last ?? _count, _count);
			T[] result = new T[take];
			for (int i = 0; i < take; i++)
				result[i] = _items[(_start + _count - take + i) % capacity];
			return result;
		}
	}

	public void Clear()
	{
		lock (_lock) { _start = 0; _count = 0; Array.Clear(_items); }
	}
}
