using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTap
{
    internal static class PosixSignals
    { 
        public delegate void SignalCallback(dynamic signalContext);
        // Microsoft has invented their own signal codes for posix signals. Normally, SIGINT is 2, and SIGTERM is 15.
        internal const int SIGINT = -2;
        internal const int SIGTERM = -4;

        // Hold on to signal handlers so they don't get disposed.
        private static List<IDisposable> SignalHandlerGcHandles = new List<IDisposable>();
        
        
        private static MethodInfo signalHandlerCreateMethod = null;
        private static MethodInfo proxyInvoke = null;
        private static Type targetDelegateType = null; 
        public static void AddSignalHandler(int signal, SignalCallback callback)
        {
            if (signalHandlerCreateMethod == null)
            {
                var asm = typeof(object).Assembly;
                var type = asm.GetType("System.Runtime.InteropServices.PosixSignalRegistration")!;
                signalHandlerCreateMethod = type.GetMethod("Create", BindingFlags.Static | BindingFlags.Public)!;
                proxyInvoke = typeof(DelegateProxy).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic)!;
                targetDelegateType = signalHandlerCreateMethod.GetParameters()[1].ParameterType;
            }

            // We cannot create a signal handler without a delegate of the correct type.
            // To get this delegate, we need to create a delegate proxy with a more relaxed signature.
            var proxy = new DelegateProxy(callback); 
            Delegate d = Delegate.CreateDelegate(targetDelegateType, proxy, proxyInvoke);
            IDisposable handler = signalHandlerCreateMethod.Invoke(null, [signal, d]) as IDisposable;
            // Finally, add the handler to a list to ensure it will not be garbage collected.
            SignalHandlerGcHandles.Add(handler);
        }

        class DelegateProxy
        {
            private SignalCallback Proxy { get; } 
            public DelegateProxy(SignalCallback callback)
            {
                Proxy = callback;
            } 
            private void Invoke(object o)
            {
                Proxy(o);
            }
        } 
    }
}