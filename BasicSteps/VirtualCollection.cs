using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenTap.Plugins.BasicSteps
{
    ///<summary> A list with virtual accessors. </summary>
    public class VirtualCollection<T> : IList<T>, IList
    {
        List<T> list = new List<T>();
        public virtual IEnumerator<T> GetEnumerator() => list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();
        public virtual void Add(T item) => list.Add(item);

        int IList.Add(object value)
        {
            Add((T) value);
            return Count - 1;
        }

        bool IList.Contains(object value) => value is T x && Contains(x);

        public virtual void Clear() => list.Clear();
        int IList.IndexOf(object value) => value is T x ? IndexOf(x) : -1;

        void IList.Insert(int index, object value) => Insert(index, (T) value);

        void IList.Remove(object value)
        {
            if (value is T x) Remove(x);
        }

        public virtual bool Contains(T item) => list.Contains(item);
        public virtual void CopyTo(T[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);
        public virtual bool Remove(T item) => list.Remove(item);
        void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

        public virtual int Count => list.Count;
        public virtual object SyncRoot => null;
        public virtual bool IsSynchronized => false;

        public virtual bool IsReadOnly => ((IList)list).IsReadOnly;
        public virtual bool IsFixedSize => ((IList)list).IsFixedSize;
        public virtual int IndexOf(T item) => list.IndexOf(item);
        public virtual void Insert(int index, T item) => list.Insert(index, item);
        public virtual void RemoveAt(int index) => list.RemoveAt(index);
        object IList.this[int index]
        {
            get => this[index];
            set => this[index] = (T) value;
        }

        public virtual T this[int index]
        {
            get => list[index];
            set => list[index] = value;
        }
    }
}