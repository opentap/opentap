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
        public object Instance { get; }
        /// <summary>
        /// The property on <see cref="Instance"/> that references the Resource
        /// </summary>
        public PropertyInfo Property { get; }
        
        /// <summary> The property that references the Resource. </summary>
        public IMemberData Member { get; }

        /// <summary> Creates an immutable instance of this class. </summary>
        public ResourceReference(object obj, PropertyInfo prop)
        {
            Instance = obj;
            Property = prop;
            Member = MemberData.Create(prop);
        }
        
        /// <summary> Creates an immutable instance of this class. </summary>
        public ResourceReference(object obj, IMemberData prop)
        {
            Instance = obj;
            Member = prop;
            if (prop is MemberData md)
                Property = md.Member as PropertyInfo;
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
            managers = PluginManager.GetPlugins<ILockManager>().OrderByDescending(t => t.GetDisplayAttribute().Order).Select(t => (ILockManager)t.CreateInstance()).ToList();
        }
        
        internal void BeforeOpen(IEnumerable<IResourceReferences> resources, CancellationToken cancellationToken)
        {
            foreach (ILockManager lockManager in managers)
            {
                lockManager.BeforeOpen(resources, cancellationToken);
            }
        }

        internal void AfterClose(IEnumerable<IResourceReferences> resources, CancellationToken cancellationToken)
        {
            foreach (ILockManager lockManager in managers.Reverse<ILockManager>())
            {
                lockManager.AfterClose(resources, cancellationToken);
            }
        }
    }
}
