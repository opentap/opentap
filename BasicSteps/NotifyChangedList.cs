using System;

namespace OpenTap.Plugins.BasicSteps
{
    /// <summary>  A list that invokes a callback when it has been changed. This is useful to handle GUIs changing the list. </summary>
    /// <typeparam name="T"></typeparam>
    class NotifyChangedList<T> : VirtualCollection<T>
    {
        public Action<NotifyChangedList<T>> ChangedCallback = list => { }; 

        public override void Insert(int index, T item)
        {
            base.Insert(index, item);
            ChangedCallback(this);
        }

        public override void Clear()
        {
            base.Clear();
            ChangedCallback(this);
        }

        public override void Add(T item)
        {
            base.Add(item);
            ChangedCallback(this);
        }

        public override bool Remove(T item)
        {
            var r= base.Remove(item);
            ChangedCallback(this);
            return r;
        }

        public override void RemoveAt(int index)
        {
            base.RemoveAt(index);
            ChangedCallback(this);
        }
    }
}