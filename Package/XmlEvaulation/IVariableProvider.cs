using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Package.XmlEvaulation
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

        /// <summary>
        /// This method is called once for each <see cref="DependsOnAttribute"/> declared on the interface implementation
        /// </summary>
        /// <param name="exp"></param>
        void InjectDependency(IElementExpander exp);
    }

    [DependsOn(typeof(PropertyExpander))]
    [DependsOn(typeof(EnvironmentVariableExpander))]
    [DependsOn(typeof(GitVersionExpander))]
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


        public void InjectDependency(IElementExpander exp)
        {

        }
    }

    [DependsOn(typeof(EnvironmentVariableExpander))]
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
                foreach (var variable in propertyGroup.Descendants())
                {
                    var k = variable.Name.LocalName;
                    // Overriding an existing key is fine here
                    // It could be intentional. E.g.
                    // <PATH>$(PATH):abc</PATH>
                    // followed by
                    // <PATH>$(PATH):def</PATH>
                    Stack.ExpandElement(variable);
                    Variables[k] = variable.Value;
                }
            }
        }

        private Dictionary<string, string> Variables { get; } = new Dictionary<string, string>();
        public ElementExpander Stack { get; set; }
        public void Expand(XElement element)
        {
            foreach (var key in Variables.Keys.OfType<string>())
            {
                ExpansionHelper.ReplaceToken(element, key, Variables[key].ToString());
            }
        }
        public void InjectDependency(IElementExpander exp)
        {
        }
    }

    [DependsOn(typeof(GitVersionExpander))]
    internal class EnvironmentVariableExpander : IElementExpander
    {
        public EnvironmentVariableExpander()
        {
            Variables = Environment.GetEnvironmentVariables();
        }

        private IDictionary Variables { get; set; }

        public void Expand(XElement element)
        {
            foreach (var key in Variables.Keys.OfType<string>())
            {
                ExpansionHelper.ReplaceToken(element, key, Variables[key].ToString());
            }
        }

        public void InjectDependency(IElementExpander exp)
        {
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

        public void InjectDependency(IElementExpander exp)
        {
        }
    }
}