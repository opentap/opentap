using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Package.XmlEvaulation
{
    internal static class ExpansionHelper
    {
        private static TraceSource log = Log.CreateSource("XML Expander");
        /// <summary>
        /// Replace all the occurrences of the token '$(token)' with 'value' in the element
        /// </summary>
        /// <param name="element"></param>
        /// <param name="token"></param>
        /// <param name="value"></param>
        internal static void ReplaceToken(XElement element, string token, string value)
        {
            var textNodes = element.Nodes().OfType<XText>().ToArray();

            foreach (var textNode in textNodes)
            {
                var curr = textNode.Value;
                var newValue = textNode.Value.Replace($"$({token})", value);

                if (curr != newValue)
                {
                    log.Debug($"Expanded '{curr}' -> '{newValue}'");
                    textNode.Value = newValue;
                }
            }

            foreach (var attribute in element.Attributes().ToArray())
            {
                var curr = attribute.Value;
                var newValue = attribute.Value.Replace($"$({token})", value);

                if (curr != newValue)
                {
                    log.Debug($"Expanded '{curr}' -> '{newValue}'");
                    attribute.Value = newValue;
                }
            }
        }
    }

    internal class ElementExpander
    {
        private static TraceSource log = Log.CreateSource($"Variable Expander");

        private List<IElementExpander> ElementExpanders = new List<IElementExpander>();
        internal void AddProvider(IElementExpander prov)
        {
            var provDeps = getDependents(prov);
            // Notify all existing expanders that depend on this of the new provider
            foreach (var expander in ElementExpanders)
            {
                var deps = getDependents(expander);

                if (deps.Any(t => t == prov.GetType()))
                    expander.InjectDependency(prov);

                if (provDeps.Contains(expander.GetType()))
                    prov.InjectDependency(expander);
            }

            ElementExpanders.Add(prov);

            isOrdered = false;
        }

        Type[] getDependents(IElementExpander e) => e.GetType().GetCustomAttributes<DependsOnAttribute>().Select(d => d.Dependent).ToArray();
        private bool isOrdered;
        void order()
        {
            var orderedExpanders = new List<IElementExpander>();
            var expanders = ElementExpanders.ToList();
            // Track progress in order to detect cycles;
            // this iterative approach is guaranteed to progress at least 1 item per iteration, but only if there are no circular dependencies.
            var progress = -1;
            while (expanders.Any())
            {
                if (orderedExpanders.Count == progress)
                {
                    var issue = string.Join(", ", expanders.Select(e => e.GetType().Name));
                    log.Debug($"Cycle detected while resolving expander order: [{issue}]");
                    throw new Exception($"Cycle detected!");
                }
                progress = orderedExpanders.Count;

                foreach (var exp in expanders.ToArray())
                {
                    var dependents = getDependents(exp);
                    var satisfied = orderedExpanders.Select(o => o.GetType());

                    if (dependents.All(d => satisfied.Contains(d)))
                    {
                        orderedExpanders.Add(exp);
                        expanders.Remove(exp);
                    }
                }
            }

            ElementExpanders = orderedExpanders;
            isOrdered = true;
        }

        internal void ExpandElement(XElement element)
        {
            if (!isOrdered) order();

            foreach (var provider in ElementExpanders)
            {
                provider.Expand(element);
            }
        }
    }
}