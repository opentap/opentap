using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OpenTap.Package
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class DependsOnAttribute : Attribute
    {
        public Type Dependent { get; set; }

        public DependsOnAttribute(Type dependent)
        {
            if (TypeData.FromType(dependent).DescendsTo(typeof(IElementExpander)) == false)
                throw new Exception($"Type '{dependent.Name}' is not an {nameof(IElementExpander)}.");
            Dependent = dependent;
        }
    }

    internal interface IElementExpander
    {
        /// <summary>
        /// Called once for each XElement in the document being expanded
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        void Expand(XElement element);
    }

    [DependsOn(typeof(PropertyExpander))]
    [DependsOn(typeof(EnvironmentVariableExpander))]
    [DependsOn(typeof(GitVersionExpander))]
    [DependsOn(typeof(DefaultVariableExpander))]
    internal class ConditionExpander : IElementExpander
    {
        private static TraceSource log = Log.CreateSource("Condition Evaluator");
        /// <summary>
        /// Evaluate any 'Condition' attribute and remove the element if it is not satisfied
        /// Otherwise remove the condition
        /// </summary>
        /// <param name="element"></param>
        public void Expand(XElement element)
        {
            foreach (var condition in element.Attributes("Condition").ToArray())
            {
                if (EvaluateCondition(condition) && condition.Parent != null)
                    condition.Remove();
                else
                {
                    if (element.Parent != null)
                        element.Remove();
                    break;
                }
            }
        }

        bool EvaluateCondition(XAttribute attr)
        {
            var condition = attr.Value;
            string normalize(string str)
            {
                // Trim leading and trailing space
                str = str.Trim();

                if (str == string.Empty) return string.Empty;

                // Remove one level of quotes if present
                if ((str[0] == '\'' || str[0] == '"') && str.First() == str.Last())
                    str = str.Substring(1, str.Length - 2);

                // Condition="true == 1" should evaluate to true
                if (str.Equals("True", StringComparison.OrdinalIgnoreCase)) return "1";
                // Condition="0 == false" should evaluate to true
                if (str.Equals("False", StringComparison.OrdinalIgnoreCase)) return "0";
                return str;
            }

            if (string.IsNullOrWhiteSpace(condition)) return false;
            try
            {
                { // Check if the condition is a literal
                    var norm = normalize(condition);

                    if (string.IsNullOrWhiteSpace(norm)) return false;
                    if (norm.Equals("1")) return true;
                    if (norm.Equals("0")) return false;
                }

                condition = condition.Trim();
                var parts = condition.Split(new string[] { "==", "!=" }, StringSplitOptions.None)
                    .Select(p => p.Trim())
                    .ToArray();

                if (parts.Length != 2)
                {
                    log.Error($"Error in condition '{condition}'. A condition must be either true, false, 1, 0, or of the form a==b.");
                    return false;
                }

                var lhs = normalize(parts[0]);
                var rhs = normalize(parts[1]);

                var isEquals = condition.IndexOf("==", StringComparison.Ordinal) >= 0;
                var areEqual = lhs.Equals(rhs, StringComparison.Ordinal);
                return isEquals == areEqual;
            }
            catch
            {
                log.Error($"Error during parsing of condition '{condition}'.");
                return false;
            }
        }
    }

    [DependsOn(typeof(GitVersionExpander))]
    internal class PropertyExpander : IElementExpander
    {

        public PropertyExpander(ElementExpander s)
        {
            Stack = s;
        }

        internal void InitVariables(IEnumerable<XElement> propertyGroups)
        {
            foreach (var propertyGroup in propertyGroups)
            {
                var desc = propertyGroup.Descendants().ToArray();
                foreach (var variable in desc)
                {
                    var k = variable.Name.LocalName;
                    // Overriding an existing key is fine here
                    // It could be intentional. E.g.
                    // <PATH>$(PATH):abc</PATH>
                    // followed by
                    // <PATH>$(PATH):def</PATH>
                    Stack.ExpandElement(variable);
                    // The variable may have been removed from the document if its condition was 'false'
                    // In this case, do not add its value as a property.
                    if (variable.Parent != null)
                        Variables[k] = variable.Value;
                }
            }
        }

        private Dictionary<string, string> Variables { get; } = new Dictionary<string, string>();
        public ElementExpander Stack { get; set; }
        public void Expand(XElement element)
        {
            foreach (var key in Variables.Keys)
            {
                ExpansionHelper.ReplaceToken(element, key, Variables[key].ToString());
            }
        }
    }

    [DependsOn(typeof(GitVersionExpander))]
    [DependsOn(typeof(PropertyExpander))]
    internal class EnvironmentVariableExpander : IElementExpander
    {
        public EnvironmentVariableExpander()
        {
            Variables = Environment.GetEnvironmentVariables();
            Keys = Variables.Keys.OfType<string>().ToArray();
        }

        private IDictionary Variables { get; }
        private string[] Keys { get; }

        public void Expand(XElement element)
        {
            foreach (var key in Keys)
            {
                ExpansionHelper.ReplaceToken(element, key, Variables[key].ToString());
            }
        }
    }

    [DependsOn(typeof(GitVersionExpander))]
    [DependsOn(typeof(PropertyExpander))]
    [DependsOn(typeof(EnvironmentVariableExpander))]
    internal class DefaultVariableExpander : IElementExpander
    {
        private static Regex VariableRegex = new Regex("\\$\\(.*?\\)");
        /// <summary>
        /// Replace all variables with an empty string. E.g. '$(whatever) -> '')'
        /// </summary>
        /// <param name="element"></param>
        public void Expand(XElement element)
        {
            var textNodes = element.Nodes().OfType<XText>().ToArray();

            foreach (var textNode in textNodes)
            {
                foreach (Match match in VariableRegex.Matches(textNode.Value))
                {
                    textNode.Value = textNode.Value.Replace(match.Value, "");
                }
            }

            foreach (var attribute in element.Attributes().ToArray())
            {
                foreach (Match match in VariableRegex.Matches(attribute.Value))
                {
                    attribute.Value = attribute.Value.Replace(match.Value, "");
                }
            }
        }
    }

    internal class GitVersionExpander : IElementExpander
    {
        public GitVersionExpander(string projectDir)
        {
            ProjectDir = projectDir;
        }

        private string ProjectDir { get; }

        private string version = null;

        public void Expand(XElement element)
        {
            if (version == null && string.IsNullOrWhiteSpace(ProjectDir) == false)
            {
                try
                {
                    var calc = new GitVersionCalulator(ProjectDir);
                    version = calc.GetVersion().ToString(5);
                }
                catch
                {
                    // fail silently
                }
            }

            // If 'GitVersion' could not be resolved, don't replace it
            if (version != null)
                ExpansionHelper.ReplaceToken(element, "GitVersion", version);
        }
    }
}