using System;
using System.IO.MemoryMappedFiles;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenTap.Plugins.BasicSteps
{
    /// <summary> String list for sharing data between threads.</summary>
    public class SharedStringList: IList<string>
    {
        // named memory mapped file.
        readonly MemoryMappedFile map;
        // system-wide mutex
        readonly Mutex mutex;
        
        public SharedStringList(string name, long maxSize = 1000000)
        {
            mutex = new Mutex(false, name + "m", out bool _);
            map = MemoryMappedFile.CreateOrOpen(name, maxSize);
        }

        public virtual Stream TransformRead(Stream inStream) => inStream;
        public virtual Stream TransformWrite(Stream inStream) => inStream;
        
        string[] read()
        {
            mutex.WaitOne();
            try
            {
                
                using (var view = map.CreateViewStream())
                {
                    using (var view2 = TransformRead(view))
                    {
                        var count = view.ReadI64Leb();
                        var strings = new string[count];
                        
                        for(var i = 0; i < count; i++)
                        {
                            var l = view2.ReadI64Leb();
                            if (l <= 0) break;
                            byte[] buffer = new byte[l];
                            int l2 = view2.Read(buffer);
                            if (l != l2) throw new Exception("Error parsing file");
                            var str = System.Text.Encoding.UTF8.GetString(buffer, 0, (int)l);
                            strings[i] = str;
                        }
                        return strings;
                    }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        void write(IEnumerable<string> strs)
        {
            mutex.WaitOne();
            using (var view = map.CreateViewStream())
            {
                using (var view2 = TransformWrite(view))
                {
                    var len = strs.Count();
                    view.WriteI64Leb(len);
                    foreach (var s in strs)
                    {
                        var l = System.Text.Encoding.UTF8.GetBytes(s);
                        view2.WriteI64Leb(l.Length);
                        view2.Write(l);
                    }
                }
            }
        }


         void withLock(Action action) => mutex.WithLock(action);
         T withLock<T>(Func<T> action) => mutex.WithLock(action);
        
        public void Add(string str)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));

            withLock(() => write(read().Append(str)));

        }

        public void Clear()
        {
            withLock(() => write(Enumerable.Empty<string>()));
        }

        public bool Contains(string item) => withLock(read).Contains(item);

        public void CopyTo(string[] array, int arrayIndex) => withLock(read).CopyTo(array, arrayIndex);

        public bool Remove(string item)
        {
            return withLock(() =>
            {
                var lst = read().ToList();
                bool removed = lst.Remove(item);
                write(lst);
                return removed;
            });
        }

        public int Count => read().Length;
        public bool IsReadOnly => false;

        public IEnumerator<string> GetEnumerator() => withLock(read).Select(x=>x).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() =>  GetEnumerator();
        public int IndexOf(string item) => withLock(read).ToList().IndexOf(item);

        public void Insert(int index, string item)
        {
            withLock(() =>
            {
                var lst = read().ToList();
                lst.Insert(index, item);
                write(lst);
            });
        }

        public void RemoveAt(int index)
        {
            withLock(() =>
            {
                var lst = read().ToList();
                lst.RemoveAt(index);
                write(lst);
            });
        }

        public string this[int index]
        {
            get => withLock(read)[index];
            set => withLock(() =>
            {
                var lst = read();
                lst[index] = value;
                write(lst);
            });
        }
    }
}