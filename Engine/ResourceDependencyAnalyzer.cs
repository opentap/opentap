//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenTap
{
    /// <summary>
    /// This class represents a resource in a dependency tree, and contains lists of the resources it depends on.
    /// </summary>
    internal class ResourceNode : IResourceReferences
    {
        /// <summary>
        /// The resource this node represents.
        /// </summary>
        public IResource Resource { get; set; }

        /// <summary>
        /// The peoperty that references this resource.
        /// </summary>
        public PropertyInfo Depender { get; set; }

        /// <summary>
        /// The resources that this node depends on. These are marked with <see cref="ResourceOpenAttribute"/>.
        /// </summary>
        internal readonly List<IResource> WeakDependencies;
        /// <summary>
        /// The resources that this node depends on. These will be opened before this nodes resource.
        /// </summary>
        internal readonly List<IResource> StrongDependencies;

        /// <summary>
        /// TestSteps (or other IResources) that uses this resource. These are the reason this resources needs to be opened when running a TestPlan.
        /// </summary>
        public List<ResourceReference> References { get; } = new List<ResourceReference>();

        internal ResourceNode(IResource resource)
        {
            this.Resource = resource;
            this.WeakDependencies = new List<IResource>();
            this.StrongDependencies = new List<IResource>();
        }

        internal ResourceNode(IResource resource, PropertyInfo prop, IEnumerable<IResource> weakDeps, IEnumerable<IResource> strongDeps)
        {
            this.Resource = resource;
            this.Depender = prop;
            this.WeakDependencies = weakDeps.ToList();
            this.StrongDependencies = strongDeps.ToList();
        }
    }


    internal class ResourceDependencyAnalyzer
    {
        private static TraceSource Log = OpenTap.Log.CreateSource("Dependency Analyzer");

        private struct ResourceDep
        {
            public readonly ResourceOpenBehavior Behavior;
            public readonly IResource Resource;
            public readonly PropertyInfo Depender; // this is important in case the resource is null

            public ResourceDep(ResourceOpenBehavior behavior, IResource resource, PropertyInfo dep)
            {
                this.Behavior = behavior;
                this.Resource = resource;
                this.Depender = dep;
            }

            public ResourceDep(IResource resource)
            {
                Depender = null;
                this.Resource = resource;
                Behavior = ResourceOpenBehavior.Before;
            }
        }

        private ResourceDep FilterProps(IResource o, PropertyInfo pi)
        {
            var behavior = ResourceOpenBehavior.Before;
            var attr = pi.GetAttribute<ResourceOpenAttribute>();
            if (attr != null) behavior = attr.Behavior;

            return new ResourceDep(behavior, o, pi);
        }

        private IEnumerable<ResourceDep> GetManyResources<T>(IList<T> steps)
        {
            if (steps.Count == 0)
                return Enumerable.Empty<ResourceDep>();
            else
            {
                return TestStepExtensions.GetObjectSettings<IResource, T, ResourceDep>(steps, true, FilterProps)
                    .Where(dep => dep.Behavior != ResourceOpenBehavior.Ignore);
            }
        }

        private IEnumerable<ResourceDep> GetResources<T>(T Step)
        {
            if (Step == null)
                return Array.Empty<ResourceDep>();
            else
                return GetManyResources(new[] { Step });
        }

        private ResourceNode Analyze(ResourceDep resource)
        {
            var resources = GetResources(resource.Resource);
            return new ResourceNode(resource.Resource,resource.Depender, 
                weakDeps: resources.Where(x => x.Behavior == ResourceOpenBehavior.InParallel).Select(x => x.Resource), 
                strongDeps: resources.Where(x => x.Behavior == ResourceOpenBehavior.Before).Select(x => x.Resource)
                );
        }

        private List<ResourceNode> GetResourceTree(IEnumerable<ResourceDep> resources)
        {
            Queue<ResourceDep> allResources = new Queue<ResourceDep>(resources);
            HashSet<ResourceDep> knownNodes = new HashSet<ResourceDep>();
            List<ResourceNode> allNodes = new List<ResourceNode>();

            ResourceDep x;
            while (allResources.Count > 0)
            {
                x = allResources.Dequeue();

                if (x.Resource == null)
                {
                    if (knownNodes.Any(k => k.Resource == x.Resource && k.Depender == x.Depender))
                        continue;
                }
                else
                {
                    if (knownNodes.Any(k => k.Resource == x.Resource))
                        continue;
                }
                knownNodes.Add(x);

                var newNode = Analyze(x);
                allNodes.Add(newNode);

                if (x.Resource != null)
                {
                    List<IResource> newNodes = newNode.WeakDependencies.Concat(newNode.StrongDependencies).ToList();
                    foreach (IResource node in newNodes.Except(knownNodes.Select(k => k.Resource)))
                        allResources.Enqueue(new ResourceDep(node));
                }
            }

            return allNodes;
        }

        private void ExpandTree(List<ResourceNode> nodes)
        {
            var lut = nodes.Where(x => x.Resource != null).ToDictionary(x => x.Resource, x => x);

            bool changed;
            do
            {
                changed = false;

                foreach (var n in nodes)
                {
                    if (n == null) continue;
                    var newDeps = n.StrongDependencies.Where(x => x != null)
                        .SelectMany(x => lut[x].StrongDependencies.Concat(lut[x].WeakDependencies))
                        .Except(n.StrongDependencies)
                        .ToList();
                    if (newDeps.Count > 0)
                    {
                        changed = true;
                        n.StrongDependencies.AddRange(newDeps);
                    }
                }
            }
            while (changed);
        }

        #region Strongly connected component algorithm
        // Can be used to find all strongly connected components(circular references) in the graph made up of the resources and the dependencies between them.
        // Implemented based on pseudo code from here: https://en.wikipedia.org/w/index.php?title=Tarjan%27s_strongly_connected_components_algorithm&oldid=774903237
        private class Vertex
        {
            internal int index, lowlink;
            internal bool onStack;
            internal ResourceNode node;
        }

        private void StrongConnect(Vertex v, Dictionary<IResource, Vertex> V, Stack<Vertex> S, List<List<IResource>> sccs, ref int index)
        {
            v.index = index;
            v.lowlink = index;
            index++;
            S.Push(v);
            v.onStack = true;

            // For all edges (v->w)
            foreach (var w in v.node.StrongDependencies.Select(n => V[n]))
            {
                if (w.index == -1)
                {
                    StrongConnect(w, V, S, sccs, ref index);
                    v.lowlink = Math.Min(v.lowlink, w.lowlink);
                }
                else if (w.onStack)
                {
                    v.lowlink = Math.Min(v.lowlink, w.index);
                }
            }

            if (v.lowlink == v.index)
            {
                List<IResource> scc = new List<IResource>();
                Vertex w;
                do
                {
                    w = S.Pop();
                    w.onStack = false;
                    scc.Add(w.node.Resource);
                }
                while (v != w);

                if (scc.Count > 1)
                    sccs.Add(scc);
            }
        }

        private List<List<IResource>> FindStronglyConnectedComponents(List<ResourceNode> tree)
        {
            List<List<IResource>> sccs = new List<List<IResource>>();

            var V = tree.Where(n => n.Resource != null).ToDictionary(n => n.Resource, n => new Vertex { index = -1, lowlink = -1, node = n });
            var S = new Stack<Vertex>();

            int index = 0;

            foreach (var v in V.Values)
                if (v.index == -1)
                    StrongConnect(v, V, S, sccs, ref index);

            return sccs;
        }
        #endregion

        /// <summary>
        /// Finds all IResource properties on a list of references. The references can be any class derived from ITapPlugin, such as ITestStep or IResource.
        /// If a reference supplied in the <paramref name="references"/> list is a IResource itself it will be added to the resulting list.
        /// </summary>
        internal List<ResourceNode> GetAllResources(List<object> references, out bool errorDetected)
        {
            errorDetected = false;

            List<ResourceDep> stepResources = GetManyResources(references).Where(x => x.Behavior != ResourceOpenBehavior.Ignore).ToList();
            List<ResourceNode> tree = GetResourceTree(references.OfType<IResource>().Select(r => new ResourceDep(r)).Concat(stepResources).ToList());

            //if (tree.Any(x => x.Resource == null))
            //    return tree;

            ExpandTree(tree);

            // Check that no resources have direct references to itself.
            foreach (var scc in tree.Where(x => x.StrongDependencies.Any(dep => dep == x.Resource)))
            {
                errorDetected = true;

                Log.Error(string.Format("Resource is referencing itself: {0}", scc.Resource.ToString()));
            }

            // Figure out if there are circular references, and list the circular references in an exception each.
            var sccs = FindStronglyConnectedComponents(tree);
            if (sccs.Any())
            {
                errorDetected = true;

                foreach (var scc in sccs)
                    Log.Error(string.Format("Circular references between resources: {0}", string.Join(",", scc.Select(res => res.Name))));
            }

            //Add users of resources
            foreach (var r in references)
            {
                TestStepExtensions.GetObjectSettings<IResource, object, ResourceNode>(new object[] { r }, true, (res, prop) =>
                   {
                       var node = tree.FirstOrDefault(n => n.Resource == res && n.Depender == prop);
                       if (node != null)
                           node.References.Add(new ResourceReference(r, prop));
                       return node;
                   });
            }
            return tree;
        }
    }
}
