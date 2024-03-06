//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace OpenTap
{
    /// <summary>
    /// Indicates how a IResource property should be handled when being opened or closed.
    /// </summary>
    public enum ResourceOpenBehavior
    {
        /// <summary>
        /// The resources pointed to by this property will be opened in sequence, so any referenced resources are open before Open() and until after Close().
        /// </summary>
        /// <remarks>This is the default behavior</remarks>
        Before,
        /// <summary>
        /// Indicates that a resource property on a resource can be opened in parallel with the resource itself.
        /// </summary>
        InParallel,
        /// <summary>
        /// Do not try to open the resource referenced by this property.
        /// </summary>
        Ignore
    }

    /// <summary>
    /// Indicates how a IResource property should be handled when being opened or closed.
    /// By default the resources will be opened in sequence, so any referenced resources are open before Open() and until after Close().
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ResourceOpenAttribute : Attribute
    {
        /// <summary>
        /// Behavior of how the resource should be handled.
        /// </summary>
        public readonly ResourceOpenBehavior Behavior;

        /// <summary>
        /// Creates a new ResourceOpen attribute for a resource property.
        /// </summary>
        /// <param name="behavior"></param>
        public ResourceOpenAttribute(ResourceOpenBehavior behavior)
        {
            this.Behavior = behavior;
        }
    }

    /// <summary>
    /// This indicates what stage a testplan execution is at.
    /// </summary>
    public enum TestPlanExecutionStage
    {
        /// <summary>
        /// Indicates that a testplan is being opened.
        /// </summary>
        Open,
        /// <summary>
        /// Indicates that a testplan is starting to execute.
        /// </summary>
        /// <remarks>Implies that the testplan already is open.</remarks>
        Execute,

        /// <summary>
        /// Indicates that a teststep is about to run it's PrePlanRun.
        /// </summary>
        PrePlanRun,
        /// <summary>
        /// Indicates that a teststep is about to be run.
        /// </summary>
        Run,
        /// <summary>
        /// Indicates that a teststep is about to run it's PostPlanRun.
        /// </summary>
        PostPlanRun,
    }

    /// <summary>
    /// A resource manager implements this interface to be able to control how resources are opened and closed during a testplan execution.
    /// </summary>
    public interface IResourceManager : ITapPlugin
    {
        /// <summary>
        /// This event is triggered when a resource is opened. The event may block in which case the resource will remain open for the entire call.
        /// </summary>
        event Action<IResource> ResourceOpened;

        /// <summary>
        /// Get a snapshot of all currently opened resources.
        /// </summary>
        IEnumerable<IResource> Resources { get; }

        /// <summary>
        /// Sets the resources that should always be opened when the testplan is.
        /// </summary>
        IEnumerable<IResource> StaticResources { get; set; }
        /// <summary>
        /// This property should be set to all teststeps that are enabled to be run.
        /// </summary>
        List<ITestStep> EnabledSteps { get; set; }

        /// <summary>
        /// Waits for all the resources that have been signalled to open to be opened.
        /// </summary>
        /// <param name="cancellationToken">Used to cancel the wait early.</param>
        void WaitUntilAllResourcesOpened(CancellationToken cancellationToken);

        /// <summary>
        /// Waits for the specific resources to be open.
        /// </summary>
        /// <param name="cancellationToken">Used to cancel the wait early.</param>
        /// <param name="targets"></param>
        void WaitUntilResourcesOpened(CancellationToken cancellationToken, params IResource[] targets);

        /// <summary>
        /// Signals that an action is beginning.
        /// </summary>
        /// <param name="planRun">The planrun for the currently executing testplan.</param>
        /// <param name="item">The item affected by the current action. This can be either a testplan or a teststep.</param>
        /// <param name="stage">The stage that is beginning.</param>
        /// <param name="cancellationToken">Used to cancel the step early.</param>
        void BeginStep(TestPlanRun planRun, ITestStepParent item, TestPlanExecutionStage stage, CancellationToken cancellationToken);

        /// <summary>
        /// Signals that an action has completed.
        /// </summary>
        /// <param name="item">The item affected by the current action. This can be either a testplan or a teststep.</param>
        /// <param name="stage">The stage that was just completed.</param>
        void EndStep(ITestStepParent item, TestPlanExecutionStage stage);
    }

    /// <summary>
    /// Utility functions shared by <see cref="ResourceTaskManager"/> and <see cref="LazyResourceManager"/>.
    /// </summary>
    internal static class ResourceManagerUtils
    {
        struct EnumerableKey
        {
            readonly int key;
            readonly object[] items;
            public EnumerableKey(object[] items)
            {
                this.items = items;
                key = 3275321;
                foreach (var item in items)
                    key = ((item?.GetHashCode() ?? 0) + key) * 3275321;
            }

            public override int GetHashCode() => key;
            public override bool Equals(object obj)
            {
                if (obj is EnumerableKey other)
                    return other.key == key && other.items.SequenceEqual(items);
                return false;
            }
        }
        
        static List<ResourceNode> GetResourceNodesNoCache(object[] source)
        {
            var resources = new ResourceDependencyAnalyzer().GetAllResources(source, out bool analysisError);
            if (analysisError)
            {
                throw new OperationCanceledException("Error while analyzing dependencies between resources.");
            }
            return resources;
        }

        class ResourceNodeCache : ICacheOptimizer
        {
            public static readonly ThreadField<Dictionary<EnumerableKey, List<ResourceNode>>> Cache = new ThreadField<Dictionary<EnumerableKey, List<ResourceNode>>>();
            public void LoadCache() => Cache.Value = new Dictionary<EnumerableKey, List<ResourceNode>>();

            public void UnloadCache() => Cache.Value = null;
        }
        
        /// <summary>
        /// Gets ResourceNodes for all resources. This includes the ones from <see cref="IResourceManager.EnabledSteps"/> and  <see cref="IResourceManager.StaticResources"/>.
        /// </summary>
        public static List<ResourceNode> GetResourceNodes(IEnumerable<object> _source)
        {
            var source = _source.ToArray();
            var cache = ResourceNodeCache.Cache.Value;
            if (cache != null)
            {
                var key = new EnumerableKey(source);
                lock (cache)
                {

                    if (cache.TryGetValue(key, out List<ResourceNode> result))
                        return result;
                    result = GetResourceNodesNoCache(source);
                    cache[key] = result;
                    return result;
                }
            }

            return GetResourceNodesNoCache(source);
        }
    }

    /// <summary>
    /// Manages the asynchronous opening and closing of <see cref="Resource"/>s in separate threads.
    /// Resources are opened and closed in order depending on dependencies between them.
    /// </summary>
    [Display("Default", Order: 0)]
    internal class ResourceTaskManager : IResourceManager
    {
        /// <summary> Prints a friendly name. </summary>
        public override string ToString() => "Default Resource Manager";
        
        static readonly TraceSource log = Log.CreateSource("Resources");
        
        readonly List<ResourceNode> openedResources = new List<ResourceNode>();
        readonly LockManager lockManager = new LockManager();
        readonly ConcurrentDictionary<IResource, Task> openTasks = new ConcurrentDictionary<IResource, Task>();

        /// <summary>
        /// This event is triggered when a resource is opened. The event may block in which case the resource will remain open for the entire call.
        /// </summary>
        public event Action<IResource> ResourceOpened;

        internal static TraceSource GetLogSource(IResource res)
        {
            return Log.GetOwnedSource(res) ?? Log.CreateSource(res.Name ?? "Resource");
        }
        
        void OpenResource(ResourceNode node, WaitHandle canStart)
        {
            canStart.WaitOne();
            var taskArray = node.StrongDependencies.Select(dep => openTasks[dep]).ToArray();
            Task.WaitAll(taskArray);

            var sw = Stopwatch.StartNew();

            var resourceLog = GetLogSource(node.Resource);

            try
            {
                ResourcePreOpenEvent.Invoke(node.Resource);
                
                // start a new thread to do synchronous work
                node.Resource.Open();

                resourceLog.Info(sw, "Resource \"{0}\" opened.", node.Resource);

                var weakDeps = node.WeakDependencies.Select(dep => openTasks[dep]).ToArray();
                Task.WaitAll(weakDeps);

                ResourceOpened?.Invoke(node.Resource);
            }
            catch (Exception ex)
            {
                string msg = $"Error while opening resource \"{node.Resource}\"";
                throw new ExceptionCustomStackTrace(msg, null, ex);
            }
        }

        /// <summary> Waits for a specific resource to be open. </summary>
        /// <param name="cancellationToken">Used to cancel the wait early.</param>
        /// <param name="targets">The resources that we wait to be opened.</param>
        public void WaitUntilResourcesOpened(CancellationToken cancellationToken, params IResource[] targets)
        {
            try
            {
                var waitFor = targets.Select(target => openTasks[target])
                    .Where(task => !task.IsCompleted || task.IsFaulted).ToArray(); // if task is faulted keep it to throw later
                if (waitFor.Length == 0) return;
                // WaitAll waits until all finish even if an exception occurs in one task.
                Task.WaitAll(waitFor, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                e.RethrowInner();
            }
        }

        /// <summary>
        /// Waits for all the resources to be opened.
        /// </summary>
        /// <param name="cancellationToken">Used to cancel the wait early</param>
        public void WaitUntilAllResourcesOpened(CancellationToken cancellationToken)
        {
            WaitUntilResourcesOpened(cancellationToken, openTasks.Keys.ToArray());
        }

        /// <summary>
        /// Get a snapshot of all currently opened resources.
        /// </summary>
        public IEnumerable<IResource> Resources => openTasks.Keys;

        /// <summary>
        /// Sets the resources that should always be opened when the test plan is.
        /// </summary>
        public IEnumerable<IResource> StaticResources { get; set; }
        /// <summary>
        /// This property should be set to all test steps that are enabled to be run.
        /// </summary>
        public List<ITestStep> EnabledSteps { get; set; }

        /// <summary>
        /// Blocks the thread while closing all resources in parallel.
        /// </summary>
         void CloseAllResources()
        {
            Dictionary<IResource, List<IResource>> dependencies =
                openedResources.ToDictionary(r => r.Resource,
                    r => new List<IResource>()); // this dictionary will hold resources with dependencies (keys) and what resources depend on them (values)
            foreach (var n in openedResources)
            foreach (var dep in n.StrongDependencies)
                dependencies[dep].Add(n.Resource);

            Dictionary<IResource, Task> closeTasks = new Dictionary<IResource, Task>();

            foreach (var r in openedResources)
                closeTasks[r.Resource] = new Task(o =>
                {
                    ResourceNode res = (ResourceNode) o;

                    // Wait for the resource to open to open before closing it.
                    // in rare cases, another instrument failing open will cause close to be called.
                    try
                    {
                        openTasks[res.Resource].Wait();
                    }
                    catch
                    {

                    }

                    // wait for resources that depend on this resource (res) to close before closing this
                    Task.WaitAll(dependencies[res.Resource].Select(x => closeTasks[x]).ToArray());
                    var resourceLog = GetLogSource(res.Resource);
                    Stopwatch timer = Stopwatch.StartNew();
                    try
                    {
                        res.Resource.Close();
                    }
                    catch (Exception e)
                    {

                        log.Error("Error while closing \"{0}\": {1}", res.Resource.Name, e.Message);
                        log.Debug(e);

                    }

                    if (resourceLog != null)
                        resourceLog.Info(timer, "Resource \"{0}\" closed.", res.Resource);
                }, r);

            var closeTaskArray = closeTasks.Values.ToArray();
            closeTaskArray.ForEach(t => t.Start());

            void complainAboutWait()
            {
                log.Debug("Waiting for resources to close:");
                foreach (var res in openTasks.Keys)
                {
                    if (res.IsConnected)
                        log.Debug(" - {0}", res);
                }
            }

            using (TimeoutOperation.Create(complainAboutWait))
            {
                Task.WaitAll(closeTaskArray);
            }
        }

        void BeginOpenResources(List<ResourceNode> resources, CancellationToken cancellationToken)
        {
            lockManager.BeforeOpen(resources, cancellationToken);

            {
                // check if any resources that has been deleted from InstrumentSettings or DutSettings
                // are still being referred to.
                var untouched = new IComponentSettingsList[]{InstrumentSettings.Current, 
                        DutSettings.Current, ResultSettings.Current}
                    .SelectMany(x => x.GetRemovedAliveResources())
                    .ToHashSet();
                foreach (var res in resources)
                {
                    if (untouched.Contains(res.Resource))
                    {
                        if(res.Depender != null)
                            throw new Exception($"Deleted resource '{res.Resource}' is in use by {res.Depender.DeclaringType}.");
                        throw new Exception($"Deleted resource '{res.Resource}' is in use.");
                    }
                }
            }

            // Check null resources
            if (resources.Any(res => res.Resource == null))
            {
                EnabledSteps.ForEach(step => step.CheckResources());

                // Now check resources since we know one of them should have a null resource
                resources.ForEach(res =>
                {
                    if (res.StrongDependencies.Contains(null) || res.WeakDependencies.Contains(null))
                        throw new Exception(String.Format("Resource property not set on resource {0}. Please configure resource.", res.Resource));
                });
            }
            else
            {
                // Open all resources asynchronously
                var wait = new ManualResetEventSlim(false);
                foreach (ResourceNode r in resources)
                {
                    if (openTasks.ContainsKey(r.Resource)) continue;

                    openedResources.Add(r);

                    // async used to avoid blocking the thread while waiting for tasks.
                    openTasks[r.Resource] = TapThread.StartAwaitable(() => OpenResource(r, wait.WaitHandle));
                }
                wait.Set();
            }
        }

        /// <summary>
        /// Signals that an action is beginning.
        /// </summary>
        /// <param name="planRun">The planrun for the currently executing testplan.</param>
        /// <param name="item">The item affected by the current action. This can be either a testplan or a teststep.</param>
        /// <param name="stage">The stage that is beginning.</param>
        /// <param name="cancellationToken">Used to cancel the step early.</param>
        public void BeginStep(TestPlanRun planRun, ITestStepParent item, TestPlanExecutionStage stage, CancellationToken cancellationToken)
        {
            switch (stage)
            {
                case TestPlanExecutionStage.Execute:
                    if (item is TestPlan testPlan)
                    {
                        var resources = ResourceManagerUtils.GetResourceNodes(StaticResources.Cast<object>().Concat(EnabledSteps));

                        // Proceed to open resources in case they have been changed or closed since last opening/executing the testplan.
                        // In case any are null, we need to do this before the resource prompt to allow a ILockManager implementation to 
                        // set the resource first.
                        if (resources.Any(r => r.Resource == null))
                            BeginOpenResources(resources, cancellationToken);

                        testPlan.StartResourcePromptAsync(planRun, resources.Select(res => res.Resource));
                        
                        if (resources.Any(r => openTasks.ContainsKey(r.Resource) == false))
                            BeginOpenResources(resources, cancellationToken); 
                    }
                    break;
                case TestPlanExecutionStage.Open:
                    if (item is TestPlan)
                    {
                        var resources = ResourceManagerUtils.GetResourceNodes(StaticResources.Cast<object>().Concat(EnabledSteps));
                        BeginOpenResources(resources, cancellationToken);
                    }
                    break;
                case TestPlanExecutionStage.Run:
                case TestPlanExecutionStage.PrePlanRun:
                    {
                        bool openCompletedWithSuccess = openTasks.Values.All(x => x.Status == TaskStatus.RanToCompletion);
                        if (!openCompletedWithSuccess)
                        {   // open did not complete or threw an exception.

                            using (TimeoutOperation.Create(() => TestPlan.PrintWaitingMessage(Resources)))
                                WaitUntilAllResourcesOpened(cancellationToken);
                        }
                        break;
                    }
                case TestPlanExecutionStage.PostPlanRun: break;
            }
        }

        /// <summary>
        /// Signals that an action has completed.
        /// </summary>
        /// <param name="item">The item affected by the current action. This can be either a testplan or a teststep.</param>
        /// <param name="stage">The stage that was just completed.</param>
        public void EndStep(ITestStepParent item, TestPlanExecutionStage stage)
        {
            switch (stage)
            {
                case TestPlanExecutionStage.Open:
                    if (item is TestPlan)
                    {
                        try
                        {
                            CloseAllResources();
                        }
                        finally
                        {
                            lockManager.AfterClose(openedResources, CancellationToken.None);
                        }
                    }
                    break;
            }
        }
    }

    
    /// <summary>
    /// Opens resources in a lazy way only before teststeps or global plan resources actually need them.
    /// </summary>
    [Display("Short Lived Connections", "Opens resources only right before they are needed (e.g. before an individual test step starts). And closes them again immediately after.", Order: 1)]
    internal class LazyResourceManager : IResourceManager
    {
        /// <summary> Prints a friendly name. </summary>
        /// <returns></returns>
        public override string ToString() => "Short Lived Connections";
        
        /// <summary> Manages the state for a single resource. For example an Instrument or Result Listener. </summary>
        class ResourceInfo
        {
            enum ResourceState
            {
                Reset,
                Opening,
                Open,
                Closing
            }

            ResourceState state { get; set; } = ResourceState.Reset;
            
            // counts up on open and down on close. When it reaches 0, it can finally be closed. 
            int referenceCount;

            // lock used for managing local state. For example reference count.
            readonly object lockObj = new object();

            Task openTask = Task.CompletedTask;
            Task closeTask = Task.CompletedTask;

            public ResourceNode ResourceNode { get; }

            public ResourceInfo(ResourceNode resourceNode)
            {
                ResourceNode = resourceNode;
            }

            public Task RequestSteady()
            {
                lock (lockObj)
                    switch(state)
                    {
                        case ResourceState.Opening:
                            return openTask;
                        default:
                            return Task.CompletedTask;
                    }
            }

            public bool ShouldBeOpen()
            {
                lock (lockObj)
                    return referenceCount > 0;
            }

            void OpenResource(LazyResourceManager requester, CancellationToken cancellationToken)
            {
                var node = ResourceNode;
                foreach (var dep in node.StrongDependencies)
                {
                    if (dep == null) continue;
                    requester.RequestResourceOpen(dep, cancellationToken).Wait(cancellationToken);
                }

                var sw = Stopwatch.StartNew();

                var resourceLog = ResourceTaskManager.GetLogSource(node.Resource);

                try
                {
                    try
                    {
                        ResourcePreOpenEvent.Invoke(node.Resource);
                        
                        node.Resource.Open();
                        resourceLog.Info(sw, "Resource \"{0}\" opened.", node.Resource);
                    }
                    finally
                    {
                        lock (lockObj)
                            if (state == ResourceState.Opening)
                                state = ResourceState.Open;
                    }

                    foreach (var dep in node.WeakDependencies)
                    {
                        if (dep == null) continue;
                        requester.RequestResourceOpen(dep, cancellationToken).Wait(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    string msg = $"Error while opening resource \"{node.Resource}\"";
                    throw new ExceptionCustomStackTrace(msg, null, ex);
                }
                requester.ResourceOpenedCallback(node.Resource);
            }

            public Task RequestOpen(LazyResourceManager requester, CancellationToken cancellationToken)
            {
                lock (lockObj)
                {
                    referenceCount++;

                    switch (state)
                    {
                        case ResourceState.Reset:
                            state = ResourceState.Opening;

                            return openTask =
                                TapThread.StartAwaitable(() => OpenResource(requester, cancellationToken));
                        case ResourceState.Opening:
                            return openTask;
                        case ResourceState.Open:
                            return Task.CompletedTask;
                        case ResourceState.Closing:
                            return closeTask.ContinueWith(t => RequestOpen(requester, cancellationToken).Wait());
                    }

                    return Task.CompletedTask;
                }
            }

            public Task RequestClose(LazyResourceManager requester)
            {
                lock (lockObj)
                {
                    referenceCount--;

                    if (referenceCount == 0)
                        switch (state)
                        {
                            case ResourceState.Reset:
                            case ResourceState.Closing:
                                throw new Exception("Should never happen");
                            case ResourceState.Opening:
                            case ResourceState.Open:
                                {
                                    state = ResourceState.Closing;

                                    return closeTask = TapThread.StartAwaitable(() =>
                                    {
                                        try
                                        {
                                            // wait for the resource to open before close.
                                            requester.resources[ResourceNode.Resource].openTask?.Wait();
                                        }
                                        catch
                                        {
                                            
                                        }

                                        Task.WaitAll(ResourceNode.WeakDependencies.Select(requester.RequestResourceClose).ToArray());
                                        var reslog = ResourceTaskManager.GetLogSource(ResourceNode.Resource);
                                        Stopwatch timer = Stopwatch.StartNew();
                                        try
                                        {
                                            ResourceNode.Resource.Close();
                                        }
                                        catch (Exception e)
                                        {
                                            reslog.Error("Error while closing \"{0}\": {1}", ResourceNode.Resource.Name, e.Message);
                                            reslog.Debug(e);
                                        }
                                        reslog.Info(timer, "Resource \"{0}\" closed.", ResourceNode.Resource);

                                        Task.WaitAll(ResourceNode.StrongDependencies.Select(requester.RequestResourceClose).ToArray());

                                        state = ResourceState.Reset;
                                    });
                                }
                        }

                    return Task.CompletedTask;
                }
            }
        }

        readonly object resourceLock = new object();
        readonly Dictionary<IResource, ResourceInfo> resources = new Dictionary<IResource, ResourceInfo>();
        readonly Dictionary<ITestStep, List<IResource>> resourceDependencies = new Dictionary<ITestStep, List<IResource>>();
        readonly List<ResourceNode> resourceWithBeforeOpenCalled = new List<ResourceNode>();
        readonly LockManager lockManager = new LockManager();

        void OpenResources(List<ResourceNode> toOpen, CancellationToken cancellationToken)
        {
            toOpen.RemoveAll(x => x.Resource == null);

            lock (resourceLock)
                foreach (var extra in toOpen)
                    if (!resources.ContainsKey(extra.Resource))
                        resources[extra.Resource] = new ResourceInfo(extra);

            try
            {
                Task.WaitAll(toOpen.Select(res => RequestResourceOpen(res.Resource, cancellationToken)).ToArray(), cancellationToken);
            }
            catch(OperationCanceledException)
            {

            }
        }

        void CloseResources(IEnumerable<IResource> toClose)
        {
            if (toClose.Any())
            {
                Task.WaitAll(toClose.Select(RequestResourceClose).ToArray());
                lock (resourceWithBeforeOpenCalled)
                {
                    var nodes = resourceWithBeforeOpenCalled.Where(rn => toClose.Contains(rn.Resource)).ToArray();
                    lockManager.AfterClose(nodes, CancellationToken.None);
                    foreach (var node in nodes)
                        resourceWithBeforeOpenCalled.Remove(node);
                }
            }
        }

        /// <summary>
        /// This property should be set to all test steps that are enabled to be run.
        /// </summary>
        public List<ITestStep> EnabledSteps { get; set; }

        /// <summary>
        /// This event is triggered when a resource is opened. The event may block in which case the resource will remain open for the entire call.
        /// </summary>
        public event Action<IResource> ResourceOpened;

        /// <summary>
        /// Get a snapshot of all currently opened resources.
        /// </summary>
        public IEnumerable<IResource> Resources
        {
            get
            {
                lock (resourceLock)
                    return resources.Where(x => x.Value.ShouldBeOpen()).Select(x => x.Key).ToList();
            }
        }

        /// <summary>
        /// Sets the resources that should always be opened when the testplan is.
        /// </summary>
        public IEnumerable<IResource> StaticResources { get; set; }

        /// <summary>
        /// Signals that an action is beginning.
        /// </summary>
        /// <param name="planRun">The planrun for the currently executing testplan.</param>
        /// <param name="item">The item affected by the current action. This can be either a testplan or a teststep.</param>
        /// <param name="stage">The stage that is beginning.</param>
        /// <param name="cancellationToken">Used to cancel the step early.</param>
        public void BeginStep(TestPlanRun planRun, ITestStepParent item, TestPlanExecutionStage stage, CancellationToken cancellationToken)
        {
            switch (stage)
            {
                case TestPlanExecutionStage.Open:
                case TestPlanExecutionStage.Execute:
                    {
                        var resources = ResourceManagerUtils.GetResourceNodes(StaticResources);

                        if (item is TestPlan plan && stage == TestPlanExecutionStage.Execute)
                        {
                            // Prompt for metadata for all resources, not only static ones.
                            var testPlanResources = ResourceManagerUtils.GetResourceNodes(EnabledSteps);
                            plan.StartResourcePromptAsync(planRun, resources.Concat(testPlanResources).Select(res => res.Resource));
                        }

                        if (resources.All(r => r.Resource?.IsConnected ?? false))
                            return;

                        // Call ILockManagers before checking for null
                        try
                        {
                            lockManager.BeforeOpen(resources, cancellationToken);
                        }
                        finally
                        {
                            lock (resourceWithBeforeOpenCalled)
                            {
                                resourceWithBeforeOpenCalled.AddRange(resources);
                            }
                        }

                        try
                        {
                            // Check null resources
                            if (resources.Any(res => res.Resource == null))
                            {
                                // Now check resources since we know one of them should have a null resource
                                resources.ForEach(res =>
                                {
                                    if (res.StrongDependencies.Contains(null) || res.WeakDependencies.Contains(null))
                                        throw new Exception(String.Format("Resource property not set on resource {0}. Please configure resource.", res.Resource));
                                });
                            }
                        }
                        finally
                        {
                            OpenResources(resources, cancellationToken);
                        }
                        break;
                    }

                case TestPlanExecutionStage.Run:
                    if (item is ITestStep step)
                    {
                        var resources = ResourceManagerUtils.GetResourceNodes(new List<object> { step });
                        if (resources.Any())
                        {
                            // Call ILockManagers before checking for null
                            try
                            {
                                lockManager.BeforeOpen(resources, cancellationToken);
                            }
                            finally
                            {
                                lock (resourceWithBeforeOpenCalled)
                                {
                                    resourceWithBeforeOpenCalled.AddRange(resources);
                                }
                            }

                            try
                            {
                                // Check null resources
                                if (resources.Any(res => res.Resource == null))
                                {
                                    step.CheckResources();

                                    // Now check resources since we know one of them should have a null resource
                                    resources.ForEach(res =>
                                    {
                                        if (res.StrongDependencies.Contains(null) || res.WeakDependencies.Contains(null))
                                            throw new Exception(String.Format("Resource property not set on resource {0}. Please configure resource.", res.Resource));
                                    });
                                }
                            }
                            finally
                            {
                                lock (resourceLock)
                                {
                                    resourceDependencies[step] = resources.Select(x => x.Resource).ToList();
                                }

                                OpenResources(resources, cancellationToken);
                            }
                            WaitHandle.WaitAny(new[] { planRun.PromptWaitHandle, planRun.MainThread.AbortToken.WaitHandle });
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Signals that an action has completed.
        /// </summary>
        /// <param name="item">The item affected by the current action. This can be either a testplan or a teststep.</param>
        /// <param name="stage">The stage that was just completed.</param>
        public void EndStep(ITestStepParent item, TestPlanExecutionStage stage)
        {
            switch (stage)
            {
                case TestPlanExecutionStage.Open:
                case TestPlanExecutionStage.Execute:
                    lock (resourceLock)
                        CloseResources(Resources);
                    break;

                case TestPlanExecutionStage.Run:
                    if (item is ITestStep step)
                    {
                        if (resourceDependencies.TryGetValue(step, out List<IResource> usedResourcesRaw))
                        {
                            var usedResources = usedResourcesRaw.Where(r => r is IResource).ToArray();
                            lock (resourceLock)
                            {
                                resourceDependencies.Remove(step);
                            }

                            CloseResources(usedResources);
                        }
                    }
                    break;
            }
        }

        private void WaitFor(CancellationToken cancellationToken, IEnumerable<IResource> targets)
        {
            try
            {
                Task[] steadyTasks;
                lock(resourceLock)
                    steadyTasks = targets.Select(x => resources[x].RequestSteady()).ToArray();

                Task.WaitAll(steadyTasks, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Just exit since the abort exception will be handled elsewhere.
            }
            catch (KeyNotFoundException)
            {
                foreach (var target in targets)
                {
                    if (resources.ContainsKey(target) == false)
                    {
                        throw new ArgumentException($"Resource '{target}' is not used in the test plan.", "targets");
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Waits for all the resources that have been signalled to open to be opened.
        /// </summary>
        /// <param name="cancellationToken">Used to cancel the wait early.</param>
        public void WaitUntilAllResourcesOpened(CancellationToken cancellationToken)
        {
            WaitFor(cancellationToken, Resources);
        }

        /// <summary>
        /// Waits for the specific resources to be open.
        /// </summary>
        /// <param name="cancellationToken">Used to cancel the wait early.</param>
        /// <param name="targets"></param>
        public void WaitUntilResourcesOpened(CancellationToken cancellationToken, params IResource[] targets)
        {
            WaitFor(cancellationToken, targets);
        }

        Task RequestResourceOpen(IResource resource, CancellationToken cancellationToken)
        {
            lock (resourceLock)
                return resources[resource].RequestOpen(this, cancellationToken);
        }

        Task RequestResourceClose(IResource resource)
        {
            lock (resourceLock)
                return resources[resource].RequestClose(this);
        }

        void ResourceOpenedCallback(IResource resource)
        {
            ResourceOpened?.Invoke(resource);
        }
    }
}
