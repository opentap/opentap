using System;

namespace OpenTap
{
    /// <summary>Owns a bootstrapped instance's lifetime. The underlying load context is not exposed.</summary>
    public sealed class ApplicationHostContext : IDisposable
    {
        IDisposable lifetime;
        object result;
        readonly object sync = new object();
        internal ApplicationHostContext(IDisposable lifetime, object result)
        {
            this.lifetime = lifetime;
            this.result = result;
        }

        /// <summary>Gets the entry point result. Values cross the context boundary unchanged;
        /// retaining hosted objects may prevent future unloading.</summary>
        /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
        public object Result
        {
            get
            {
                lock (sync)
                {
                    if (lifetime == null) throw new ObjectDisposedException(nameof(ApplicationHostContext));
                    return result;
                }
            }
        }

        /// <summary>
        /// Notifies the hosted instance, requests cooperative cancellation of its TapThreads,
        /// releases engine logging resources and adapter references, and requests unloading.
        /// Plugin resource cleanup and completion of non-cooperative work remain the application's responsibility.
        /// This operation is idempotent and does not guarantee context unloading or collection.
        /// </summary>
        public void Dispose()
        {
            IDisposable cleanup;
            lock (sync)
            {
                cleanup = lifetime;
                lifetime = null;
                result = null;
            }
            cleanup?.Dispose();
        }
    }
}
