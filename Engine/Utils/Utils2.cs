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
            del += () => f(v);
            return del;
        }
    }
}
