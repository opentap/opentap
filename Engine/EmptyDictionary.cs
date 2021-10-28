using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    class EmptyDictionary<K, V> : IDictionary<K, V>
    {
        private EmptyDictionary() { }
        public static EmptyDictionary<K, V> Instance { get; } = new EmptyDictionary<K, V>();
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => Enumerable.Empty<KeyValuePair<K,V>>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Add(KeyValuePair<K, V> item) => throw new NotSupportedException();
        public void Clear() { }
        public bool Contains(KeyValuePair<K, V> item) => false;
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex) { }
        public bool Remove(KeyValuePair<K, V> item) => false;
        public int Count => 0;
        public bool IsReadOnly => true;
        public void Add(K key, V value) => throw new NotSupportedException();
        public bool ContainsKey(K key) => false;
        public bool Remove(K key) => false;
        public bool TryGetValue(K key, out V value)
        {
            value = default;
            return false;
        }
        public V this[K key]
        {
            get => throw new KeyNotFoundException();
            set => throw new NotSupportedException();
        }
        public ICollection<K> Keys => Array.Empty<K>();
        public ICollection<V> Values => Array.Empty<V>();
    }
}