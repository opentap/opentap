//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap
{
    /// <summary>
    /// Represents a reference to a resource. A reference is defined as something in the TestPlan that references a Resource, and thus causes it to get opened. Used by <see cref="IResourceReferences"/>
    /// </summary>
    public class ResourceReference
    {
        /// <summary>
        /// The TestStep or other Resource that is using some Resource
        /// </summary>
        public object Instance { get; private set; }
        /// <summary>
        /// The property on <see cref="Instance"/> that references the Resource
        /// </summary>
        public PropertyInfo Property { get; private set; }

        /// <summary>
        /// Creates an immutable instance of this class.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="prop"></param>
        public ResourceReference(object obj, PropertyInfo prop)
        {
            Instance = obj;
            Property = prop;
        }
    }

    /// <summary>
    /// Used in <see cref="ILockManager"/> to represents a resource and its references
    /// </summary>
    public interface IResourceReferences
    {
        /// <summary>
        /// The resource this item represents.
        /// </summary>
        IResource Resource { get; set; }
        /// <summary>
        /// References to <see cref="Resource"/> from TestSteps (or other Resources). These references are the reason this resources needs to be opened when running a TestPlan.
        /// </summary>
        List<ResourceReference> References { get; }
    }

    /// <summary>
    /// Implementing this interface will enable hooks before and after resources are opened and closed.
    /// </summary>
    public interface ILockManager : ITapPlugin
    {
        /// <summary>
        /// This hook is triggered before <see cref="IResource.Open"/> is executed. Only called once when e.g. a TestStep with mulitple resources starts.
        /// </summary>
        /// <param name="resources">The resources that will be opened.</param>
        /// <param name="abortToken">A token that will be signalled if the locking action should be cancelled.</param>
        void BeforeOpen(IEnumerable<IResourceReferences> resources, CancellationToken abortToken);

        /// <summary>
        /// This hook is triggered after the TestStep or TestPlan is done executing. If triggered by a TestStep, defered actions may still be running.
        /// </summary>
        /// <param name="resources">This will contain the same resources as given to <see cref="BeforeOpen"/>".</param>
        /// <param name="abortToken">A token that will be signalled if the locking action should be cancelled.</param>
        void AfterClose(IEnumerable<IResourceReferences> resources, CancellationToken abortToken);
    }

    internal class LockManager
    {
        private List<ILockManager> managers;

        public LockManager()
        {
            managers = PluginManager.GetPlugins<ILockManager>().Select(t => (ILockManager)t.CreateInstance()).ToList();
        }

        void managerAction(IEnumerable<IResourceReferences> resources, CancellationToken cancellationToken, Action<ILockManager> f)
        {
            if (resources.Count() == 0) return;

            try
            {
                if (managers.Count > 1)
                    managers.AsParallel().WithCancellation(cancellationToken).ForAll(f);
                else
                    managers.ForEach(f);
            }
            catch (OperationCanceledException)
            { }
        }

        internal void BeforeOpen(IEnumerable<IResourceReferences> resources, CancellationToken cancellationToken)
        {
            managerAction(resources, cancellationToken, t => t.BeforeOpen(resources, cancellationToken));
        }

        internal void AfterClose(IEnumerable<IResourceReferences> resources, CancellationToken cancellationToken)
        {
            managerAction(resources,cancellationToken, t => t.AfterClose(resources, cancellationToken));
        }
    }
}
