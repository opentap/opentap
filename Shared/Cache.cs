using System;
using System.Collections.Immutable;
namespace OpenTap
{
    /// <summary> Cache class which clears when a certain criteria is met. </summary>
    internal class Cache<K, V>
    {
        // using immutable dictionary to avoid issues with race conditions.
        // note that for caches data races are not that important.
        ImmutableDictionary<K, V> dict = ImmutableDictionary<K, V>.Empty;
        
        // this function returns an object which is used for checking if the cache should be cleared.
        readonly Func<object> clearTrigger;
        
        // last object returned by clearTrigger.
        object currentObj;

        ///<summary> The current number of cached elements. </summary> 
        public int Count
        {
            get
            {
                CheckClear();
                return dict.Count;
            }
        }

        public Cache(Func<object> clearTrigger)
        {
            this.clearTrigger = clearTrigger;
            currentObj = clearTrigger();
        }
        
        void CheckClear()
        {
            var clearCheck = clearTrigger();
            if (Equals(clearCheck, currentObj) == false)
            {
                dict = ImmutableDictionary<K, V>.Empty;
                currentObj = clearTrigger();
            }
        }

        /// <summary> tries getting a value for a key. </summary>
        public bool TryGetValue(K key, out V value)
        {
            CheckClear();
            return dict.TryGetValue(key, out value);
        }

        public V AddValue(K key, V value)
        {
            CheckClear();
            dict = dict.Add(key, value);
            return value;
        }
    }
}
