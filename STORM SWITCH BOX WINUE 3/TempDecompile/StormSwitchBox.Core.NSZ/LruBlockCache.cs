using System.Collections.Generic;

namespace StormSwitchBox.Core.NSZ;

public class LruBlockCache
{
	private readonly int _capacity;

	private readonly Dictionary<int, LinkedListNode<(int Index, byte[] Data)>> _cacheMap;

	private readonly LinkedList<(int Index, byte[] Data)> _lruList;

	private readonly object _lock = new object();

	public LruBlockCache(int capacity)
	{
		_capacity = capacity;
		_cacheMap = new Dictionary<int, LinkedListNode<(int, byte[])>>(capacity);
		_lruList = new LinkedList<(int, byte[])>();
	}

	public byte[]? Get(int index)
	{
		lock (_lock)
		{
			if (_cacheMap.TryGetValue(index, out LinkedListNode<(int, byte[])> value))
			{
				_lruList.Remove(value);
				_lruList.AddFirst(value);
				return value.Value.Item2;
			}
			return null;
		}
	}

	public void Put(int index, byte[] data)
	{
		lock (_lock)
		{
			if (_cacheMap.TryGetValue(index, out LinkedListNode<(int, byte[])> value))
			{
				value.Value = (index, data);
				_lruList.Remove(value);
				_lruList.AddFirst(value);
				return;
			}
			if (_cacheMap.Count >= _capacity)
			{
				LinkedListNode<(int, byte[])> last = _lruList.Last;
				if (last != null)
				{
					_cacheMap.Remove(last.Value.Item1);
					_lruList.RemoveLast();
				}
			}
			LinkedListNode<(int, byte[])> linkedListNode = new LinkedListNode<(int, byte[])>((index, data));
			_lruList.AddFirst(linkedListNode);
			_cacheMap.Add(index, linkedListNode);
		}
	}
}
