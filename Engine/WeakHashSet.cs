using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

class WeakHashSet<T> where T: class
{
    readonly ConditionalWeakTable<T, object> table = new ConditionalWeakTable<T, object>();

    private readonly List<WeakReference<T>> lst = new List<WeakReference<T>>(); 
    static object value = new object();
    public bool Contains(T obj) => table.TryGetValue(obj, out var _);

    public bool Add(T obj)
    {
        if (table.TryGetValue(obj, out var _))
            return false;
        table.Add(obj, value);
        var free = lst.FirstOrDefault(x => x.TryGetTarget(out _) == false);
        if(free != null)
            free.SetTarget(obj);
        else
            lst.Add(new WeakReference<T>(obj));
        return true;
    }

    public IEnumerable<T> GetElements()
    {
        foreach (var item in lst)
        {
            if (item.TryGetTarget(out T x))
                yield return x;
        }
    }
}
