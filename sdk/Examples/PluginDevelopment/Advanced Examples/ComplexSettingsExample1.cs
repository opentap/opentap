//Copyright 2012-2019 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenTap;

namespace PluginDevelopment.Advanced_Examples
{
    // This file shows an example of how to build an object that has more than usually complex data
    // and make it editable and serializable in a normal way.
    // There are more ways than this, but this is probably the simplest ways of making it.
    
    /// <summary> This settings element has some rather complex behaviors. For example it needs to know which DUT instance
    /// owns it.</summary>
    public class ComplexSettingsElement1 : INotifyPropertyChanged
    {
        /// <summary>
        /// In order to have the right AvailableInts, we need the DUT value to always be up to date.
        /// </summary>
        public IEnumerable<int> AvailableInts => Dut?.AvailableInts ?? Enumerable.Empty<int>();

        int a;

        [AvailableValues(nameof(AvailableInts))]
        public int A
        {
            get => a;
            set
            {
                if (a == value) return;
                a = value;
                OnPropertyChanged();
            }
        }

        int b;
        [AvailableValues(nameof(AvailableInts))]
        public int B
        {
            get => b;
            set
            {
                if (b == value) return;
                b = value;
                OnPropertyChanged();
            }
        }

        internal ComplexSettingsExample1 Dut;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    [Display("Complex Settings DUT1", "Demonstrates how to use a list where items cannot be added or removed." +
                                       " Elements themselves can be changed, but they are also mostly read-only.",
        Groups: new[] { "Examples", "Plugin Development", "Advanced Examples" })]
    public class ComplexSettingsExample1 : Dut
    {
        public List<int> AvailableInts { get; set; }

        NotifyList<ComplexSettingsElement1> elements = new NotifyList<ComplexSettingsElement1>();
        public NotifyList<ComplexSettingsElement1> Elements
        {
            get
            {
                elementsNotifyChanged(elements);
                elements.NotifyChanged = elementsNotifyChanged;
                elements.ElementChanged = onElementChanged;
                return elements;
            }
            set => elements = (value as NotifyList<ComplexSettingsElement1>) ?? new NotifyList<ComplexSettingsElement1>(value);
        }

        void onElementChanged(NotifyList<ComplexSettingsElement1> list, ComplexSettingsElement1 element1)
        {
            // insert logic to handle changes in the element
            Log.Debug("Element {0} changed", list.IndexOf(element1) + 1);
        }

        void elementsNotifyChanged(NotifyList<ComplexSettingsElement1> list)
        {
            foreach (var elem in list)
                elem.Dut = this;
        }
    }

    /// <summary>  List type that is easily extendible. </summary>
    public class VirtualList<T> : IList<T>, IList
    {
        List<T> elements = new List<T>();
        public virtual IEnumerator<T> GetEnumerator() => elements.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() =>  GetEnumerator();
        public void Add(T item) => Insert(Count, item);
        public int Add(object value)
        {
            Add((T) value);
            return Count - 1;
        }

        public virtual bool Contains(object value) => ((IList)elements).Contains(value);
        public virtual void Clear() => elements.Clear();
        public int IndexOf(object value) =>  IndexOf((T) value);
        public void Insert(int index, object value) => Insert(index, (T) value);
        public void Remove(object value) => Remove((T) value);
        public virtual bool Contains(T item) => elements.Contains(item);
        public virtual void CopyTo(T[] array, int arrayIndex) => elements.CopyTo(array, arrayIndex);

        public bool Remove(T item)
        {
            var idx = IndexOf(item);
            if (idx == -1) return false;
            RemoveAt(idx);
            return true;
        }
        public void CopyTo(Array array, int index) => CopyTo((T[]) array, index);
        public virtual int Count => elements.Count;
        public object SyncRoot => false;
        public bool IsSynchronized => false;
        public virtual bool IsReadOnly => ((IList) elements).IsReadOnly;
        public bool IsFixedSize => false;
        public virtual int IndexOf(T item) => elements.IndexOf(item);
        public virtual void Insert(int index, T item) => elements.Insert(index, item);
        public virtual void RemoveAt(int index) => elements.RemoveAt(index);
        object IList.this[int index]
        {
            get => this[index];
            set => this[index] = (T) value;
        }

        public virtual T this[int index]
        {
            get => elements[index];
            set => elements[index] = value;
        }
    }

    /// <summary> A type of list that can emit notifications on change.</summary>
    public class NotifyList<T> : VirtualList<T>
    {
        public NotifyList(IEnumerable<T> initialElements = null)
        {
            foreach (var elem in initialElements ?? Enumerable.Empty<T>())
                Add(elem);
        }

        public NotifyList()
        {
            
        }
        // Invoked when the list has changed.
        public Action<NotifyList<T>> NotifyChanged { get; set; }
        /// <summary> Invoked when an element of the list has changed. </summary>
        public Action<NotifyList<T>, T> ElementChanged { get; set; }
        bool notify = true;

        void notifyOnChanged()
        {
            if(notify)
                NotifyChanged?.Invoke(this);
        }

        public override void Insert(int index, T item)
        {
            base.Insert(index, item);
            if (item is INotifyPropertyChanged n)
                n.PropertyChanged += elementPropertyChanged;
            notifyOnChanged();
        }

        void elementPropertyChanged(object sender, PropertyChangedEventArgs e) => ElementChanged?.Invoke(this, (T)sender);

        public override T this[int index]
        {
            get => base[index];
            set
            {
                notify = false;
                try
                {
                    RemoveAt(index);
                }
                finally
                {
                    notify = true;
                }

                Insert(index, value);
            }
        }

        public override void Clear()
        {
            base.Clear();
            foreach (var item in this)
            {
                if (item is INotifyPropertyChanged n)
                    n.PropertyChanged -= elementPropertyChanged;
            }
            notifyOnChanged();
        }

        public override void RemoveAt(int index)
        {
            if (index >= Count) return;
            var elem = this[index];
            if (elem is INotifyPropertyChanged n)
                n.PropertyChanged -= elementPropertyChanged;
            base.RemoveAt(index);
            notifyOnChanged();
        }
    }
}