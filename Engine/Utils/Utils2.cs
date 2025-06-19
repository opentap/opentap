using System;
using System.Collections.Generic;
namespace OpenTap
{
    /// <summary>
    /// Named Utils2 to avoid clashing with Utils from the Shared project.
    /// </summary>
    static class Utils2
    {
        public static bool IsLooped(List<(Guid, Guid)> waitingFor, Guid item)
        {
            Stack<Guid> stack = new Stack<Guid>();
            stack.Push(item);
            HashSet<Guid> visited = new HashSet<Guid>();
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }
                foreach (var (waiter, waited) in waitingFor)
                {
                    if (waiter == current)
                    {
                        if (waited == item)
                            return true;
                        stack.Push(waited);
                    }
                }
            }


            return false;
        }

        public static Action Bind<T>(this Action del, Action<T> f, T v)
        {
            if (del == null)
                return () => f(v);

            del += () => f(v);
            return del;
        }

        public static bool IsSortedBy<T>(this IList<T> values, Func<T, double> desc)
        {
            var n = values.Count;
            if (n <= 1) return true;
            
            double v0 = desc(values[0]);
            for (int i = 1; i < n; i++)
            {
                var v1 = desc(values[i]);
                if (v0 > v1)
                    return false;
                
                v0 = v1;
            }

            return true;
        }
        
        public static void Shuffle<T>(this List<T> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                
                (list[k], list[n]) = (list[n], list[k]);
            }
        }   
    }
}
