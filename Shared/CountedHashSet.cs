//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary> This set contains the N most recently used and most used elements for type T. The capacity is set upon initialization and cannot be changed. </summary>
    /// <typeparam name="T"></typeparam>
    internal class CountedHashSet<T>
    {
        private class SetItem
        {
            public int Uses;
            public ulong LastUse;

            public void Use(ulong usageIndex)
            {
                Uses++;
                LastUse = usageIndex;
            }

            public SetItem(ulong usageIndex)
            {
                Uses = 1;
                LastUse = usageIndex;
            }

            public override string ToString()
            {
                return $"{Uses} : {LastUse}";
            }

        }

        private int capacity;
        private int trimThreshold;
        ulong usageIndex = 0;
        private Dictionary<T, SetItem> knownSet = new Dictionary<T, SetItem>();

        private void Trim(int newCount)
        {
            if (knownSet.Count + newCount >= trimThreshold)
            {
                int removeCount = (knownSet.Count + newCount) - capacity;

                var toRemove = knownSet.OrderBy(kvp => kvp.Value.Uses).ThenBy(kvp => kvp.Value.LastUse).Take(removeCount).Select(x => x.Key).ToList();
                for (int i = 0; i < toRemove.Count; i++)
                    knownSet.Remove(toRemove[i]);
            }
        }

        /// <summary>
        /// Creates a new counted hash set. Items will be removed based on use count, oldest first, when the number of items reaches the <paramref name="threshold"/> until there are only <paramref name="desiredCapacity"/> items left.
        /// </summary>
        /// <param name="desiredCapacity">This is the capacity the set should at most have when trimmed.</param>
        /// <param name="threshold">This is the capacity the set will have when a trimming operation will start.</param>
        public CountedHashSet(int desiredCapacity = 1024 * 8, int threshold = 1024 * 10)
        {
            if (desiredCapacity >= threshold) throw new ArgumentException("Capacity should be smaller than the threshold", nameof(desiredCapacity));
            if (threshold <= 1) throw new ArgumentException("threshold should be larger than 1", nameof(threshold));

            this.capacity = desiredCapacity;
            this.trimThreshold = threshold;
        }

        public void Add(T item)
        {
            if (knownSet.TryGetValue(item, out SetItem value))
                value.Use(usageIndex++);
            else
            {
                Trim(1);
                knownSet.Add(item, new SetItem(usageIndex++));
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            HashSet<T> toAdd = null; // hashset to avoid duplicates
            foreach (var item in items)
            {
                if (knownSet.TryGetValue(item, out SetItem value))
                    value.Use(usageIndex++);
                else
                {
                    if (toAdd == null)
                        toAdd = new HashSet<T>();
                    toAdd.Add(item);       
                }
            }
            if(toAdd != null){
                Trim(toAdd.Count);
                foreach (var item in toAdd)
                    knownSet.Add(item, new SetItem(usageIndex++));
            }
            
        }

        public bool Contains(T item)
        {
            if (knownSet.TryGetValue(item, out SetItem value))
            {
                value.Use(usageIndex++);
                return true;
            }
            else
                return false;
        }

        public int UsageCount(T item)
        {
            if (knownSet.TryGetValue(item, out SetItem value))
                return value.Uses;
            else
                return 0;
        }

        public void Clear()
        {
            knownSet.Clear();
            usageIndex = 0;
        }
    }
}
