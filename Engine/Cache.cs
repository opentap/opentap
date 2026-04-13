using System;
using System.Collections.Immutable;
namespace OpenTap
{
    class CacheObservable
    {
        public event EventHandler Updated; 

        public void OnUpdated()
        {
            Updated?.Invoke(this, EventArgs.Empty);
        }
    }

    internal class Cached<K>
    {
        readonly Func<K> getValue;
        K value;
        bool hasValue;

        public K GetValue()
        {
            if (!hasValue)
            {
                value = getValue();
                hasValue = true;
            }
            return value;
        }
        public Cached(CacheObservable cacheState, Func<K> getValue)
        {
            this.getValue = getValue;
            cacheState.Updated += CacheStateOnChange;
        }
        void CacheStateOnChange(object sender, EventArgs e)
        {
            hasValue = false;
        }
    }
}
