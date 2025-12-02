using System;
using System.Collections.Generic;
using System.Text;

namespace RandomImageScreensaverWPF
{
    public class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _cacheMap;
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _lruList;

        public LruCache(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            }
            _capacity = capacity;
            _cacheMap = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();
            _lruList = new LinkedList<KeyValuePair<TKey, TValue>>();
        }

        public TValue? Get(TKey key)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddLast(node);
                return node.Value.Value;
            }
            return default;
        }

        public void Put(TKey key, TValue value)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                node.Value = new KeyValuePair<TKey, TValue>(key, value);
                _lruList.Remove(node);
                _lruList.AddLast(node);
            }
            else
            {
                if (_cacheMap.Count >= _capacity)
                {
                    var lruNode = _lruList.First;
                    if (lruNode != null)
                    {
                        _cacheMap.Remove(lruNode.Value.Key);
                        _lruList.RemoveFirst();
                    }
                }

                var newNode = new LinkedListNode<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value));
                _lruList.AddLast(newNode);
                _cacheMap.Add(key, newNode);
            }
        }
    }
}
